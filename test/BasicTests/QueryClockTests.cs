using AwesomeAssertions;
using KustoLoco.Core;
using KustoLoco.Core.Evaluation;

namespace BasicTests;

/// <summary>
///     now() and ago() read the context's injectable clock, once per query.
///     https://learn.microsoft.com/kusto/query/now-function
///     https://learn.microsoft.com/kusto/query/ago-function
///     The filter results were checked against the ADX engine (kustainer), with the fixture rows placed relative
///     to the instant the comparison ran.
/// </summary>
[TestClass]
public class QueryClockTests : TestMethods
{
    private static readonly DateTime Anchor = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    // rows 30 minutes, 2 hours and 3 days before the anchor, and 10 minutes after it
    private const string Table = """
                                 let T = datatable(Id:long, TimeGenerated:datetime) [
                                 1, datetime(2026-03-01T11:30:00Z),
                                 2, datetime(2026-03-01T10:00:00Z),
                                 3, datetime(2026-02-26T12:00:00Z),
                                 4, datetime(2026-03-01T12:10:00Z)
                                 ];
                                 """;

    private static async Task<KustoQueryResult> RunAt(Func<DateTime> clock, string query)
    {
        var context = CreateContext();
        context.Clock = clock;
        var result = await context.RunQuery(query);
        result.Error.Should().BeEmpty();
        return result;
    }

    [TestMethod]
    public async Task NowAndAgo_ReadTheInjectedClock()
    {
        var result = await RunAt(() => Anchor, "print n = now(), a = ago(1h), p = now(-1d), f = now(2h)");
        result.GetRow(0).Should().Equal(
            new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 28, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 1, 14, 0, 0, DateTimeKind.Utc));
    }

    [TestMethod]
    public async Task Clock_IsReadOncePerQuery()
    {
        var ticks = 0;
        DateTime Advancing() => Anchor.AddSeconds(Interlocked.Increment(ref ticks));
        var result = await RunAt(Advancing,
            "range i from 1 to 5000 step 1 | extend a = ago(0s), n = now() " +
            "| summarize da = dcount(a), dn = dcount(n), same = countif(a == n)");
        string.Join(",", result.GetRow(0)).Should().Be("1,1,5000");
        ticks.Should().Be(1);
    }

    [TestMethod]
    public void QueryClock_OutsideAQueryReadsTheSystemClock()
    {
        var before = DateTime.UtcNow;
        QueryClock.UtcNow.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        using (QueryClock.Fix(Anchor))
            QueryClock.UtcNow.Should().Be(Anchor);
        QueryClock.UtcNow.Should().NotBe(Anchor);
    }

    [TestMethod]
    public async Task Clock_DefaultsToTheSystemClock()
    {
        var before = DateTime.UtcNow;
        var result = await CreateContext().RunQuery("print now()");
        ((DateTime)result.GetRow(0)[0]!).Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [TestMethod]
    [DataRow("T | where TimeGenerated > ago(1h) | project Id", "1,4")]
    [DataRow("T | where TimeGenerated between (ago(1d) .. now()) | project Id", "1,2")]
    [DataRow("T | where TimeGenerated > now(-4d) and TimeGenerated < now(-1d) | project Id", "3")]
    [DataRow("T | extend age = now() - TimeGenerated | where age > 1h | project Id, age", "2,02:00:00,3,3.00:00:00")]
    [DataRow("let lookback = 2d; T | where TimeGenerated >= ago(lookback + 1d) | count", "4")]
    public async Task Filters_UseTheInjectedClock(string query, string expected)
    {
        var context = CreateContext();
        context.Clock = () => Anchor;
        var result = await context.RunQuery(Table + query);
        result.Error.Should().BeEmpty();
        string.Join(",", result.EnumerateRows().Select(RowString)).Should().Be(expected);
    }
}
