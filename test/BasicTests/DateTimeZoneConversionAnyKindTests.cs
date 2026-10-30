using System.Globalization;
using AwesomeAssertions;
using KustoLoco.Core;
using KustoLoco.Core.Console;
using KustoLoco.Core.Settings;
using KustoLoco.FileFormats;

namespace BasicTests;

/// <summary>
/// ADX's <c>datetime_local_to_utc</c> reinterprets the wall clock of ANY datetime in the named zone, and
/// <c>datetime_utc_to_local</c> reads ANY datetime as UTC. KustoLoco passed the input straight to
/// <c>TimeZoneInfo.ConvertTimeToUtc</c> / <c>ConvertTimeFromUtc</c>, which throw when the value's
/// <see cref="DateTimeKind"/> does not match the zone (Utc or Local into ConvertTimeToUtc for a zone that is
/// neither UTC nor the host's; Local into ConvertTimeFromUtc), and the catch turned that into null.
///
/// Only Unspecified values (bare literals) converted. But make_datetime, todatetime, column type inference
/// and the min/max aggregates all produce Utc-kind values, and datetime_utc_to_local itself returns
/// Local-kind values, so the documented round trip returned null as well. Each cell asserts the input's
/// kind (datetime_kind) next to the ADX result. The cells do not depend on the host's time zone.
/// </summary>
[TestClass]
public class DateTimeZoneConversionAnyKindTests : TestMethods
{
    public DateTimeZoneConversionAnyKindTests()
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-GB");
    }

    // 2020-02-02 20:02:20 read in US/Pacific (PST, UTC-8) is 2020-02-03 04:02:20 UTC.

    [TestMethod]
    public async Task LocalToUtc_OfMakeDatetime_ReinterpretsWallClock()
    {
        var query = """
                    print local_dt = make_datetime(2020, 2, 2, 20, 2, 20)
                    | project input_kind = datetime_kind(local_dt), utc_dt = datetime_local_to_utc(local_dt, 'US/Pacific')
                    """;
        (await ResultAsString(query)).Should().Be("Utc,2020-02-03 04:02:20Z");
    }

    [TestMethod]
    public async Task LocalToUtc_OfInferredNoOffsetColumn_ReinterpretsWallClock()
    {
        // Column type inference stamps a no-offset datetime as Utc (AssumeUniversal | AdjustToUniversal).
        var console = new SystemConsole();
        var csv = """
                  local_dt
                  2020-02-02 20:02:20
                  """;
        var table = CsvSerializer.Default(new KustoSettingsProvider(), console).LoadFromString(csv, "data");
        table = TableBuilder.AutoInferColumnTypes(table, console);
        var context = CreateContext();
        context.AddTable(table);

        var result = await context.RunQuery(
            "data | project input_kind = datetime_kind(local_dt), utc_dt = datetime_local_to_utc(local_dt, 'US/Pacific')");

        result.Error.Should().BeEmpty();
        RowString(result.GetRow(0)).Should().Be("Utc,2020-02-03 04:02:20Z");
    }

    [TestMethod]
    public async Task LocalToUtc_OfMaxAggregate_ReinterpretsWallClock()
    {
        var query = """
                    datatable(local_dt: datetime) [ datetime(2020-02-02 20:02:20) ]
                    | summarize m = max(local_dt)
                    | project input_kind = datetime_kind(m), utc_dt = datetime_local_to_utc(m, 'US/Pacific')
                    """;
        (await ResultAsString(query)).Should().Be("Utc,2020-02-03 04:02:20Z");
    }

    [TestMethod]
    public async Task LocalToUtc_InvertsUtcToLocal()
    {
        // datetime_utc_to_local returns a Local-kind value; converting it back must return the original instant.
        var query = """
                    print t = datetime(2020-02-03 04:02:20)
                    | extend local_dt = datetime_utc_to_local(t, 'US/Pacific')
                    | project input_kind = datetime_kind(local_dt), back = datetime_local_to_utc(local_dt, 'US/Pacific')
                    """;
        (await ResultAsString(query)).Should().Be("Local,2020-02-03 04:02:20Z");
    }

    [TestMethod]
    public async Task UtcToLocal_OfLocalKindValue_ReadsItAsUtc()
    {
        // The mirror case: datetime_utc_to_local must read its input as UTC whatever its kind. The inner call
        // returns a Local-kind 2020-02-02 20:02:20; read as UTC that is 21:02:20 in Europe/Paris (CET, UTC+1).
        var query = """
                    print t = datetime(2020-02-03 04:02:20)
                    | extend local_dt = datetime_utc_to_local(t, 'US/Pacific')
                    | project input_kind = datetime_kind(local_dt), paris = datetime_utc_to_local(local_dt, 'Europe/Paris')
                    """;
        (await ResultAsString(query)).Should().Be("Local,2020-02-02 21:02:20.0000");
    }
}
