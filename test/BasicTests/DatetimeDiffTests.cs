using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     datetime_diff counts the period boundaries crossed between the two values.
///     https://learn.microsoft.com/kusto/query/datetime-diff-function
///     Every expected value here was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class DatetimeDiffTests : TestMethods
{
    private const string Periods =
        "year,quarter,month,week,day,hour,minute,second,millisecond,microsecond,nanosecond";

    // expected: one value per period, in the order of Periods
    [TestMethod]
    [DataRow("2024-01-01 10:01:59", "2024-01-01 10:00:01", "0,0,0,0,0,0,1,118,118000,118000000,118000000000")]
    [DataRow("2024-01-01 10:00:01", "2024-01-01 10:01:59", "0,0,0,0,0,0,-1,-118,-118000,-118000000,-118000000000")]
    [DataRow("2024-01-01 00:00:00.0000001", "2023-12-31 23:59:59.9999999", "1,1,1,0,1,1,1,1,1,1,200")]
    [DataRow("2023-12-31 23:59:59.9999999", "2024-01-01 00:00:00.0000001", "-1,-1,-1,0,-1,-1,-1,-1,-1,-1,-200")]
    [DataRow("2024-03-31 00:00:00", "2024-04-01 00:00:00", "0,-1,-1,0,-1,-24,-1440,-86400,-86400000,-86400000000,-86400000000000")]
    [DataRow("2024-04-01 00:00:00", "2024-03-31 23:59:59", "0,1,1,0,1,1,1,1,1000,1000000,1000000000")]
    [DataRow("2024-01-07 00:00:00", "2024-01-06 23:59:59", "0,0,0,1,1,1,1,1,1000,1000000,1000000000")]
    [DataRow("2024-01-08 00:00:00", "2024-01-07 00:00:00", "0,0,0,0,1,24,1440,86400,86400000,86400000000,86400000000000")]
    [DataRow("2024-01-13 23:00:00", "2024-01-07 01:00:00", "0,0,0,0,6,166,9960,597600,597600000,597600000000,597600000000000")]
    [DataRow("2024-01-06 00:00:00", "2024-01-14 00:00:00", "0,0,0,-2,-8,-192,-11520,-691200,-691200000,-691200000000,-691200000000000")]
    [DataRow("2017-10-30 23:05:01", "2017-10-30 23:00:59", "0,0,0,0,0,0,5,242,242000,242000000,242000000000")]
    [DataRow("2025-06-15 12:34:56.7891234", "2019-02-28 01:02:03.4567891", "6,25,76,329,2299,55187,3311252,198675173,198675173333,198675173332334,198675173332334300")]
    [DataRow("2019-02-28 01:02:03.4567891", "2025-06-15 12:34:56.7891234", "-6,-25,-76,-329,-2299,-55187,-3311252,-198675173,-198675173333,-198675173332334,-198675173332334300")]
    [DataRow("2024-01-01 00:00:00.0015", "2024-01-01 00:00:00.0005", "0,0,0,0,0,0,0,0,1,1000,1000000")]
    [DataRow("2024-01-01 00:00:00.0000015", "2024-01-01 00:00:00.0000005", "0,0,0,0,0,0,0,0,0,1,1000")]
    [DataRow("2024-01-01 00:00:00.0000005", "2024-01-01 00:00:00.0000015", "0,0,0,0,0,0,0,0,0,-1,-1000")]
    [DataRow("2024-12-31 00:00:00", "2024-10-01 00:00:00", "0,0,2,13,91,2184,131040,7862400,7862400000,7862400000000,7862400000000000")]
    [DataRow("2024-10-01 00:00:00", "2024-09-30 23:59:59", "0,1,1,0,1,1,1,1,1000,1000000,1000000000")]
    [DataRow("2024-01-01 00:00:00", "2024-01-01 00:00:00", "0,0,0,0,0,0,0,0,0,0,0")]
    public async Task EveryPeriodCountsBoundaries(string a, string b, string expected)
    {
        var columns = string.Join(", ",
            Periods.Split(',').Select(p => $"{p}=datetime_diff('{p}', a, b)"));
        var query = $"print a=datetime({a}), b=datetime({b}) | project {columns}";
        (await ResultAsString(query)).Should().Be(expected);
    }

    [TestMethod]
    public async Task MinuteIsBoundariesNotElapsedTime()
    {
        var result = await ResultAsString(
            "print m=datetime_diff('minute', datetime(2024-01-01 10:01:59), datetime(2024-01-01 10:00:01))");
        result.Should().Be("1");
    }

    [TestMethod]
    public async Task PeriodNameIsCaseInsensitive()
    {
        var result = await ResultAsString(
            "print a=datetime_diff('Minute', datetime(2024-01-01 10:01:59), datetime(2024-01-01 10:00:01)), " +
            "b=datetime_diff('MINUTE', datetime(2024-01-01 10:01:59), datetime(2024-01-01 10:00:01))");
        result.Should().Be("1,1");
    }

    [TestMethod]
    public async Task WorksOnColumns()
    {
        var result = await ResultAsString("""
            datatable(a:datetime, b:datetime)[
              datetime(2024-01-01 10:01:59), datetime(2024-01-01 10:00:01),
              datetime(2024-12-31), datetime(2024-10-01),
              datetime(2024-01-07), datetime(2024-01-06 23:59:59)]
            | project q=datetime_diff('quarter', a, b), w=datetime_diff('week', a, b), m=datetime_diff('minute', a, b)
            """);
        result.Should().Be("0,0,1,0,13,131040,0,1,1");
    }
}
