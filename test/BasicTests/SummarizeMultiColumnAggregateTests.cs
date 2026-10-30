using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     A summarize may hold an aggregate that returns several columns (arg_min, arg_max) beside other aggregates,
///     in any order; each aggregate's columns follow the previous aggregate's.
///     https://learn.microsoft.com/kusto/query/arg-max-aggregate-function
///     Every expected value was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class SummarizeMultiColumnAggregateTests : TestMethods
{
    private const string T = "datatable(k:string, t:long, v:string, w:string)['a',1,'x','p','a',2,'y','q','b',5,'z','r'] ";

    [TestMethod]
    [DataRow(T + "| summarize arg_min(t, v), arg_max(t, w) by k | order by k asc", "a,1,x,2,q;b,5,z,5,r", "k,t,v,t1,w")]
    [DataRow(T + "| summarize arg_min(t, v), L = arg_max(t, w) by k | order by k asc", "a,1,x,2,q;b,5,z,5,r", "k,t,v,L,w")]
    [DataRow(T + "| summarize A = arg_min(t, v), L = arg_max(t, w) by k | order by k asc", "a,1,x,2,q;b,5,z,5,r", "k,A,v,L,w")]
    [DataRow(T + "| summarize arg_max(t, v), n = count() by k | order by k asc", "a,2,y,2;b,5,z,1", "k,t,v,n")]
    [DataRow(T + "| summarize n = count(), arg_max(t, v), m = max(t) by k | order by k asc", "a,2,2,y,2;b,1,5,z,5", "k,n,t,v,m")]
    [DataRow(T + "| summarize arg_max(t, v), arg_min(t, w)", "5,z,1,p", "t,v,t1,w")]
    [DataRow(T + "| summarize arg_max(t, v) by k | order by k asc", "a,2,y;b,5,z", "k,t,v")]
    public async Task EachAggregatesColumnsFollowThePreviousAggregates(string query, string rows, string columns)
    {
        var result = await CreateContext().RunQuery(query);
        result.Error.Should().BeEmpty();
        string.Join(";", result.EnumerateRows().Select(RowString)).Should().Be(rows);
        string.Join(",", result.ColumnNames()).Should().Be(columns);
    }
}
