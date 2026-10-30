using System;
using NotNullStrings;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

[KustoImplementation(Keyword = "Functions.DatetimeUtcToLocal")]
internal partial class DateTimeUtcToLocalFunction
{
    private static DateTime? Impl(DateTime input, string tz)
    {
        if (tz.IsBlank())
            return null;
        try
        {
            var tzi =TimeZoneInfo.FindSystemTimeZoneById(tz);
            // ADX reads the input as UTC whatever its kind. ConvertTimeFromUtc throws for a Local-kind
            // value (e.g. this function's own output), so read the ticks as UTC explicitly.
            var d = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(input, DateTimeKind.Utc), tzi);
            return DateTime.SpecifyKind(d, DateTimeKind.Local);
        }
        catch
        {
            return null;
        }
    }
}

[KustoImplementation(Keyword = "Functions.DatetimeLocalToUtc")]
internal partial class DateTimeLocalToUtcFunction
{
    private static DateTime? Impl(DateTime input, string tz)
    {
        if (tz.IsBlank())
            return null;
        try
        {
            var tzi = TimeZoneInfo.FindSystemTimeZoneById(tz);
            // ADX reinterprets the wall clock of ANY datetime in the named zone. ConvertTimeToUtc throws for
            // a Utc- or Local-kind value whose kind does not match the zone (make_datetime, todatetime, column
            // inference and min/max all produce Utc; datetime_utc_to_local produces Local), so drop the kind.
            var d = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(input, DateTimeKind.Unspecified), tzi);
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }
        catch
        {
            return null;
        }
    }
}


