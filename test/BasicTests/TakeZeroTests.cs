using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     take with a count of zero, or a negative count, returns no rows with the input's columns, whatever produced
///     the input. https://learn.microsoft.com/kusto/query/take-operator
///     A where (or a join, a lookup...) hands the next operator remapped columns; take 0 over them failed with
///     "Operation is not valid due to the current state of the object", and a negative count failed while the
///     result was read. Every expected value was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class TakeZeroTests : TestMethods
{
    private const string Table = "let T = datatable(a:long, s:string)[1, 'x', 2, 'y', 3, 'z'];";

    [TestMethod]
    [DataRow("T | where a > 0 | take 0", "", "a,s")]
    [DataRow("T | where a > 1 | take 0 | project s", "", "s")]
    [DataRow("T | where a > 0 | take 0 | count", "0", "Count")]
    [DataRow("T | where a > 0 | limit 0 | extend b = a * 2", "", "a,s,b")]
    [DataRow("T | where a > 0 | take 0 | summarize n = count()", "0", "n")]
    [DataRow("T | where a > 0 | take 0 | union (T | where a == 2)", "2,y", "a,s")]
    [DataRow("T | take 0", "", "a,s")]
    [DataRow("T | take 1 | take 0", "", "a,s")]
    [DataRow("T | where a > 5 | take 0", "", "a,s")]
    [DataRow("T | where a > 0 | take -1", "", "a,s")]
    [DataRow("T | take -1", "", "a,s")]
    [DataRow("T | where a > 0 | take 2", "1,x,2,y", "a,s")]
    public async Task TakeZeroOrLess_ReturnsNoRowsWithTheInputColumns(string query, string rows, string columns)
    {
        var result = await CreateContext().RunQuery(Table + query);
        result.Error.Should().BeEmpty();
        string.Join(",", result.EnumerateRows().Select(RowString)).Should().Be(rows);
        string.Join(",", result.ColumnNames()).Should().Be(columns);
    }
}
