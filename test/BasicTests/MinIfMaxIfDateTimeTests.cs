using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     minif / maxif over a datetime: the first or last time a condition held.
///     https://learn.microsoft.com/kusto/query/minif-aggregate-function
///     Every expected value was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class MinIfMaxIfDateTimeTests : TestMethods
{
    private const string T =
        "datatable(k:string, t:datetime, ok:bool)['a',datetime(2015-01-02),true,'a',datetime(2015-01-01),false," +
        "'a',datetime(2015-01-03),true,'b',datetime(2015-01-05),false] ";

    [TestMethod]
    public async Task MinIfAndMaxIfOverADateTime()
    {
        var result = await CreateContext().RunQuery(T + "| summarize mn = minif(t, ok), mx = maxif(t, ok) by k | order by k asc");
        result.Error.Should().BeEmpty();
        var rows = result.EnumerateRows().ToArray();
        rows.Length.Should().Be(2);
        rows[0][1].Should().Be(new DateTime(2015, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        rows[0][2].Should().Be(new DateTime(2015, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        rows[1][1].Should().BeNull();
        rows[1][2].Should().BeNull();
    }
}
