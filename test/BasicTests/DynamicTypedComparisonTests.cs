using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     ADX compares a dynamic value with a typed scalar - a number, a bool, a datetime, a timespan or a guid - by
///     converting the dynamic value to the scalar's type, as the matching to-function does (tolong, todouble,
///     todatetime...), so p.n == 123 holds for the string "0123" and p.t == datetime(2015-01-01) for the text
///     "2015-01-01T00:00:00Z". A comparison with a string still compares text.
///     https://learn.microsoft.com/kusto/query/scalar-data-types/dynamic
///     Every expected value was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class DynamicTypedComparisonTests : TestMethods
{
    private const string Values =
        "print p = dynamic({\"a\":\"0123\",\"c\":\" 123\",\"d\":\"abc\",\"e\":\"123\",\"f\":123,\"g\":1.5,\"h\":\"1.50\",\"j\":\"true\",\"b\":true," +
        "\"x\":\"2015-01-01T10:20:30Z\",\"s\":\"abc\",\"z\":null,\"u\":\"a0e6d7d4-1c9b-4a1d-9c3e-0f1e2d3c4b5a\"," +
        "\"U\":\"A0E6D7D4-1C9B-4A1D-9C3E-0F1E2D3C4B5A\",\"t\":\"1.00:00:00\"}) | project r = ";

    private async Task<string> Compare(string comparison) => await ResultAsString(Values + comparison);

    [TestMethod]
    [DataRow("p.a == 123", "True")]
    [DataRow("p.c == 123", "True")]
    [DataRow("p.e == 123", "True")]
    [DataRow("p.d == 123", "False")]
    [DataRow("p.f == 123.0", "True")]
    [DataRow("p.e == 123.0", "True")]
    [DataRow("p.h == 1.5", "True")]
    [DataRow("p.g == 1.5", "True")]
    [DataRow("p.e == int(123)", "True")]
    [DataRow("p.e == decimal(123)", "True")]
    [DataRow("123 == p.e", "True")]
    [DataRow("p.e != 123", "False")]
    [DataRow("p.d != 123", "True")]
    [DataRow("p.j == true", "True")]
    [DataRow("p.b == true", "True")]
    public async Task DynamicEqualsANumberOrABool(string comparison, string expected)
    {
        (await Compare(comparison)).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("p.a > 100", "True")]
    [DataRow("p.e < 200", "True")]
    [DataRow("p.g > 1", "False")]
    [DataRow("p.e <= 123", "True")]
    [DataRow("p.e >= 124", "False")]
    [DataRow("100 < p.e", "True")]
    [DataRow("p.e > int(100)", "True")]
    [DataRow("isnull(p.d < 1)", "True")]
    public async Task DynamicOrderedAgainstANumber(string comparison, string expected)
    {
        (await Compare(comparison)).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("p.x == datetime(2015-01-01T10:20:30Z)", "True")]
    [DataRow("datetime(2015-01-01T10:20:30Z) == p.x", "True")]
    [DataRow("p.x != datetime(2015-01-01T10:20:30Z)", "False")]
    [DataRow("p.x == datetime(2015-01-01T10:20:31Z)", "False")]
    [DataRow("p.s == datetime(2015-01-01)", "False")]
    [DataRow("p.z == datetime(2015-01-01)", "False")]
    [DataRow("p.absent == datetime(2015-01-01)", "False")]
    [DataRow("p.absent != datetime(2015-01-01)", "True")]
    public async Task DynamicEqualsADateTime(string comparison, string expected)
    {
        (await Compare(comparison)).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("p.u == guid(a0e6d7d4-1c9b-4a1d-9c3e-0f1e2d3c4b5a)", "True")]
    [DataRow("p.U == guid(a0e6d7d4-1c9b-4a1d-9c3e-0f1e2d3c4b5a)", "True")]
    [DataRow("p.t == 1d", "True")]
    public async Task DynamicComparedWithAGuidOrATimespan(string comparison, string expected)
    {
        (await Compare(comparison)).Should().Be(expected);
    }

    [TestMethod]
    public async Task ArrayElementEqualsADateTime()
    {
        (await ResultAsString("print p = dynamic([\"2015-01-01T10:20:30Z\"]) | project r = p[0] == datetime(2015-01-01T10:20:30Z)"))
            .Should().Be("True");
    }

    [TestMethod]
    [DataRow("datatable(d:dynamic)[dynamic({\"t\":\"2015-01-01T10:20:30Z\"}), dynamic({\"t\":\"2016-01-01T00:00:00Z\"}), " +
             "dynamic({\"t\":null})] | where d.t == datetime(2016-01-01) | count", "1")]
    [DataRow("datatable(d:dynamic)[dynamic({\"n\":\"5\"}), dynamic({\"n\":7}), dynamic({\"n\":\"x\"})] | where d.n > 4 | count", "2")]
    public async Task DynamicColumnFilteredByATypedValue(string query, string expected)
    {
        (await ResultAsString(query)).Should().Be(expected);
    }

    // a string compared with a typed scalar compares the text of the scalar, unchanged by this
    [TestMethod]
    [DataRow("print r = '123' == 123", "True")]
    [DataRow("print r = '0123' == 123", "False")]
    [DataRow("print r = 'abc' == 123", "False")]
    [DataRow("print r = 123 == '123'", "True")]
    [DataRow("print r = 'True' == true", "True")]
    public async Task StringComparedWithATypedValueComparesText(string query, string expected)
    {
        (await ResultAsString(query)).Should().Be(expected);
    }

    // a dynamic compared with a string compares its text
    [TestMethod]
    [DataRow("p.f == '0123'", "False")]
    [DataRow("p.g == '1.50'", "False")]
    [DataRow("p.b == 'true'", "True")]
    [DataRow("p.b == 'True'", "False")]
    public async Task DynamicComparedWithAStringComparesText(string comparison, string expected)
    {
        (await Compare(comparison)).Should().Be(expected);
    }
}
