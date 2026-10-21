using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     A where whose predicate is constant false returns no rows, with the input's columns, rather than failing.
///     https://learn.microsoft.com/kusto/query/where-operator
///     The row and column expectations were checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class WhereConstantFalseTests : TestMethods
{
    private const string Table = "let T = datatable(Id:long, S:string) [1, 'a', 2, 'b'];";

    [TestMethod]
    [DataRow("T | where false")]
    [DataRow("T | where false | project Id")]
    [DataRow("T | where 1 == 2 | summarize count()")]
    [DataRow("T | where false | extend x = strlen(S) | project Id, x")]
    [DataRow("T | where false | join kind=inner (T) on Id")]
    [DataRow("T | where false | union (T | where Id == 1)")]
    [DataRow("let e = T | where false; e | count")]
    public async Task ConstantFalse_DoesNotFail(string query)
    {
        var result = await CreateContext().RunQuery(Table + query);
        result.Error.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("T | where false | project Id", "", "Id")]
    [DataRow("T | where false", "", "Id,S")]
    [DataRow("T | where 1 == 2 | summarize n = count()", "0", "n")]
    [DataRow("T | where false | union (T | where Id == 1) | project Id", "1", "Id")]
    public async Task ConstantFalse_RowsAndColumns(string query, string rows, string columns)
    {
        var result = await CreateContext().RunQuery(Table + query);
        result.Error.Should().BeEmpty();
        string.Join(",", result.EnumerateRows().Select(RowString)).Should().Be(rows);
        string.Join(",", result.ColumnNames()).Should().Be(columns);
    }
}
