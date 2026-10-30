using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     row_cumsum(term [, restart]): the restart argument is optional.
///     https://learn.microsoft.com/kusto/query/row-cumsum-function
///     Every expected value was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class RowCumSumOptionalRestartTests : TestMethods
{
    [TestMethod]
    [DataRow("datatable(a:long)[1,2,3] | serialize c = row_cumsum(a)", "1,1;2,3;3,6")]
    [DataRow("datatable(a:long)[1,2,3] | serialize | extend c = row_cumsum(a)", "1,1;2,3;3,6")]
    [DataRow("datatable(a:long)[3,1,2] | sort by a asc | extend c = row_cumsum(a)", "1,1;2,3;3,6")]
    [DataRow("datatable(a:int)[int(1),int(2)] | serialize c = row_cumsum(a)", "1,1;2,3")]
    [DataRow("datatable(a:real)[1.5,2.5] | serialize c = row_cumsum(a)", "1.5,1.5;2.5,4")]
    [DataRow("datatable(a:long)[1,2,3] | serialize | extend s = iff(a > 1, 1, 0) | extend c = row_cumsum(s) | project a, c",
        "1,0;2,1;3,2")]
    [DataRow("datatable(a:long)[1,2,3,4] | serialize c = row_cumsum(a, a == 3)", "1,1;2,3;3,3;4,7")]
    public async Task RowCumSum(string query, string expected)
    {
        (await ResultAsString(query, ";")).Should().Be(expected);
    }
}
