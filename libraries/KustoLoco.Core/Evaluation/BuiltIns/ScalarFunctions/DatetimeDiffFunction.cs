using System;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

/// <summary>
///     datetime_diff(period, a, b): the number of period boundaries crossed going from b to a
/// </summary>
/// <remarks>
///     https://learn.microsoft.com/kusto/query/datetime-diff-function
///     Both values are truncated to the start of their period and the truncated values are
///     subtracted, so 10:01:59 - 10:00:01 is 1 minute and 23:59:59.9999999 to 00:00:00.0000001
///     on the next day is 1 for every period from day up to microsecond. Weeks start on Sunday.
/// </remarks>
[KustoImplementation(Keyword = "Functions.DatetimeDiff")]
internal partial class DatetimeDiffFunction
{
    private static long Impl(string period, DateTime a, DateTime b)
    {
        return period.ToLowerInvariant() switch
        {
            DateTimeParts.Year => a.Year - b.Year,
            DateTimeParts.Quarter => DatetimeDiffPeriods.QuarterIndex(a) - DatetimeDiffPeriods.QuarterIndex(b),
            DateTimeParts.Month => (a.Year - b.Year) * 12L + a.Month - b.Month,
            DateTimeParts.Week => DatetimeDiffPeriods.WeekIndex(a) - DatetimeDiffPeriods.WeekIndex(b),
            DateTimeParts.Day => DatetimeDiffPeriods.Boundaries(a, b, TimeSpan.TicksPerDay),
            DateTimeParts.Hour => DatetimeDiffPeriods.Boundaries(a, b, TimeSpan.TicksPerHour),
            DateTimeParts.Minute => DatetimeDiffPeriods.Boundaries(a, b, TimeSpan.TicksPerMinute),
            DateTimeParts.Second => DatetimeDiffPeriods.Boundaries(a, b, TimeSpan.TicksPerSecond),
            DateTimeParts.Millisecond => DatetimeDiffPeriods.Boundaries(a, b, TimeSpan.TicksPerMillisecond),
            DateTimeParts.Microsecond => DatetimeDiffPeriods.Boundaries(a, b, DatetimeDiffPeriods.TicksPerMicrosecond),
            DateTimeParts.Nanosecond => (a.Ticks - b.Ticks) * 100,
            _ => 0
        };
    }
}

/// <summary>
///     Helpers for <see cref="DatetimeDiffFunction" />. They live outside it because the source
///     generator copies Impl into separate generated classes.
/// </summary>
internal static class DatetimeDiffPeriods
{
    public const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    // DateTime ticks are never negative, so integer division truncates each value
    // to the start of its period
    public static long Boundaries(DateTime a, DateTime b, long ticksPerPeriod)
        => a.Ticks / ticksPerPeriod - b.Ticks / ticksPerPeriod;

    public static long QuarterIndex(DateTime d) => d.Year * 4L + (d.Month - 1) / 3;

    // 0001-01-01 (day 0) is a Monday; shifting by one day makes each index start on a Sunday
    public static long WeekIndex(DateTime d) => (d.Ticks / TimeSpan.TicksPerDay + 1) / 7;
}
