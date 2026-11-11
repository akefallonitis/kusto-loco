using AwesomeAssertions;
using KustoLoco.Core;
using KustoLoco.Core.Evaluation;

namespace BasicTests;

/// <summary>
///     The expansion row budget (<see cref="KustoQueryContext.MaxExpandedRows"/>): the rows mv-expand and mv-apply emit across a
///     whole query are charged, and past the budget the query stops — before building the rows past it — with a typed
///     <see cref="ExpansionBudgetExceededException"/> on the result, never a silently truncated result. Unset, nothing changes;
///     the budget is per query; an empty array emits (and charges) no row, a non-array value one.
/// </summary>
[TestClass]
public class ExpansionBudgetTests
{
    // 3 rows holding 4, 2 and 0 elements, and one scalar row: 4 + 2 + 0 + 1 = 7 expanded rows.
    private const string Source = "datatable(a:dynamic)[dynamic([1,2,3,4]), dynamic([5,6]), dynamic([]), dynamic(\"x\")]";

    private static Task<KustoQueryResult> Run(string query, long? budget) =>
        new KustoQueryContext { MaxExpandedRows = budget }.RunQuery(query);

    [TestMethod]
    [DataRow(Source + " | mv-expand a | count")]
    [DataRow(Source + " | mv-apply e = a on (summarize n = count()) | count")]
    public async Task Past_the_budget_the_query_stops_with_the_typed_exception(string query)
    {
        var result = await Run(query, budget: 5);
        result.Exception.Should().BeOfType<ExpansionBudgetExceededException>();
        ((ExpansionBudgetExceededException)result.Exception!).Budget.Should().Be(5);
        result.Error.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task At_the_budget_the_query_runs_and_counts_every_expanded_row()
    {
        var result = await Run(Source + " | mv-expand a | count", budget: 7);
        result.Error.Should().BeEmpty();
        result.Exception.Should().BeNull();
        Convert.ToInt64(result.EnumerateRows().Single().ToArray()[0]).Should().Be(7);
    }

    [TestMethod]
    public async Task Unset_nothing_is_charged()
    {
        var result = await Run(Source + " | mv-expand a | count", budget: null);
        result.Error.Should().BeEmpty();
        Convert.ToInt64(result.EnumerateRows().Single().ToArray()[0]).Should().Be(7);
    }

    [TestMethod]
    public async Task The_budget_is_per_query()
    {
        var context = new KustoQueryContext { MaxExpandedRows = 7 };
        (await context.RunQuery(Source + " | mv-expand a | count")).Error.Should().BeEmpty();
        (await context.RunQuery(Source + " | mv-expand a | count")).Error.Should().BeEmpty();   // a fresh budget, not a running total
    }

    [TestMethod]
    public async Task Nested_expansions_charge_every_level()
    {
        // level one: 2 rows; level two: 2 + 1 = 3 rows ⇒ 5 charged in all.
        var query = "datatable(f:dynamic)[dynamic([{\"i\":[1,2]},{\"i\":[3]}])] | mv-expand g = f | mv-expand h = g.i | count";
        (await Run(query, budget: 5)).Error.Should().BeEmpty();
        (await Run(query, budget: 4)).Exception.Should().BeOfType<ExpansionBudgetExceededException>();
    }
}
