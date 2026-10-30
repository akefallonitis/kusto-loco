using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     == and != with one null operand are false and true, as in ADX; with two null operands, and for the ordered
///     comparisons, the result stays null. So `where a != 5` keeps the rows whose a is null.
///     https://learn.microsoft.com/kusto/query/null-values
///     Every expected value was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class EqualityWithNullTests : TestMethods
{
    [TestMethod]
    [DataRow("long(null) == 1", "False")]
    [DataRow("long(null) != 1", "True")]
    [DataRow("1 == long(null)", "False")]
    [DataRow("int(null) == 1", "False")]
    [DataRow("int(null) != int(1)", "True")]
    [DataRow("real(null) == 1.5", "False")]
    [DataRow("decimal(null) == decimal(1)", "False")]
    [DataRow("bool(null) == true", "False")]
    [DataRow("bool(null) != true", "True")]
    [DataRow("datetime(null) == datetime(2015-01-01)", "False")]
    [DataRow("datetime(null) != datetime(2015-01-01)", "True")]
    [DataRow("timespan(null) == 1d", "False")]
    [DataRow("guid(null) == guid(a0e6d7d4-1c9b-4a1d-9c3e-0f1e2d3c4b5a)", "False")]
    [DataRow("isnull(long(null) == long(null))", "True")]
    [DataRow("isnull(long(null) != long(null))", "True")]
    [DataRow("isnull(datetime(null) == datetime(null))", "True")]
    [DataRow("isnull(long(null) < 1)", "True")]
    [DataRow("isnull(long(null) >= 1)", "True")]
    [DataRow("long(2) == 2", "True")]
    [DataRow("long(2) != 2", "False")]
    public async Task ComparisonWithNull(string comparison, string expected)
    {
        (await ResultAsString($"print r = {comparison}")).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("datatable(a:long)[1, long(null), 5] | where a != 5 | count", "2")]
    [DataRow("datatable(a:long)[1, long(null), 5] | where a == 5 | count", "1")]
    [DataRow("datatable(t:datetime)[datetime(2015-01-01), datetime(null)] | where t != datetime(2015-01-01) | count", "1")]
    [DataRow("datatable(b:bool)[true, false, bool(null)] | where b != true | count", "2")]
    public async Task FilterOnNotEqualKeepsNullRows(string query, string expected)
    {
        (await ResultAsString(query)).Should().Be(expected);
    }
}
