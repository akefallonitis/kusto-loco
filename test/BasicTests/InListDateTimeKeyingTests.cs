using AwesomeAssertions;
using KustoLoco.Core;
using KustoLoco.Core.Console;
using KustoLoco.Core.Settings;
using KustoLoco.FileFormats;

namespace BasicTests;

/// <summary>
///     <c>in</c> over datetimes must agree with <c>==</c>: KQL defines <c>x in (a, b)</c> as <c>x == a or x == b</c>,
///     and KustoLoco compares datetimes by their ticks whatever their <see cref="DateTimeKind"/>.
///     The in-list key applied ToUniversalTime() to every DateTime. That shifts an Unspecified value (a bare literal)
///     by the host's UTC offset but leaves a Utc value (make_datetime, todatetime, column inference, min/max) alone,
///     so on any non-UTC host a value and a list element denoting the same instant got different keys and the row
///     was silently dropped. UTC hosts mask it.
///     https://learn.microsoft.com/kusto/query/in-operator
/// </summary>
[TestClass]
public class InListDateTimeKeyingTests : TestMethods
{
    [TestMethod]
    // a Utc-kind value in a list of bare (Unspecified) literals
    [DataRow("print T = make_datetime(2020, 2, 2, 20, 2, 20) | where T in (datetime(2020-02-02 20:02:20), datetime(2021-01-01 00:00:00)) | count", "1")]
    [DataRow("print T = todatetime('2020-02-02 20:02:20') | where T in (datetime(2020-02-02 20:02:20), datetime(2021-01-01 00:00:00)) | count", "1")]
    [DataRow("datatable(D:datetime) [ datetime(2020-02-02 20:02:20) ] | summarize T = max(D) | where T in (datetime(2020-02-02 20:02:20), datetime(2021-01-01 00:00:00)) | count", "1")]
    // a bare value in a list holding a Utc-kind element, or a string element
    [DataRow("datatable(T:datetime) [ datetime(2020-02-02 20:02:20) ] | where T in (make_datetime(2020, 2, 2, 20, 2, 20), datetime(2021-01-01 00:00:00)) | count", "1")]
    [DataRow("""datatable(T:datetime) [ datetime(2020-02-02 20:02:20) ] | where T in (dynamic(["2020-02-02 20:02:20", "2021-01-01 00:00:00"])) | count""", "1")]
    // guards that already held: the same kind on both sides, and a datetime_utc_to_local (Local-kind) value whose
    // wall clock equals the bare literals; == compares it by ticks, so in must not reinterpret it as host-local
    [DataRow("datatable(T:datetime) [ datetime(2020-02-02 20:02:20) ] | where T in (datetime(2020-02-02 20:02:20), datetime(2021-01-01 00:00:00)) | count", "1")]
    [DataRow("print T = datetime_utc_to_local(datetime(2020-02-03 04:02:20), 'US/Pacific') | where T in (datetime(2020-02-02 20:02:20), datetime(2021-01-01 00:00:00)) | count", "1")]
    public async Task In_DateTime_MatchesTheSameInstantWhateverItsKind(string query, string expected)
    {
        (await LastLineOfResult(query)).Should().Be(expected);
    }

    [TestMethod]
    public async Task In_InferredNoOffsetColumn_MatchesBareLiterals()
    {
        // Column type inference stamps a no-offset datetime as Utc (AssumeUniversal | AdjustToUniversal).
        var console = new SystemConsole();
        var csv = """
                  T
                  2020-02-02 20:02:20
                  """;
        var table = CsvSerializer.Default(new KustoSettingsProvider(), console).LoadFromString(csv, "data");
        table = TableBuilder.AutoInferColumnTypes(table, console);
        var context = CreateContext();
        context.AddTable(table);

        var result = await context.RunQuery(
            "data | where T in (datetime(2020-02-02 20:02:20), datetime(2021-01-01 00:00:00)) | count");

        result.Error.Should().BeEmpty();
        RowString(result.GetRow(0)).Should().Be("1");
    }
}
