using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     in / !in / in~ against a dynamic literal compare with its values as written, although the
///     literal, used as a value, types its datetime and guid strings (DynamicStringTypingTests).
///     https://learn.microsoft.com/kusto/query/in-operator
///     https://learn.microsoft.com/kusto/query/scalar-data-types/dynamic
///     Every expected value here was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class DynamicStringTypingInListTests : TestMethods
{
    private const string UpperGuid = "A0E6D7D4-1C9B-4A1D-9C3E-0F1E2D3C4B5A";
    private const string Guid = "a0e6d7d4-1c9b-4a1d-9c3e-0f1e2d3c4b5a";

    [TestMethod]
    public async Task InListMatchesADynamicLiteralAsWritten()
    {
        var result = await ResultAsString(
            $$"""print a = "2015-01-01" in (dynamic(["2015-01-01"])), b = "2015-01-01T00:00:00.0000000Z" in (dynamic(["2015-01-01"])), c = "{{UpperGuid}}" in (dynamic(["{{UpperGuid}}"])), d = "{{Guid}}" in (dynamic(["{{UpperGuid}}"])), e = "x" in (dynamic(["2015-01-01", "x"])), f = "{{UpperGuid}}" in ("x", dynamic(["{{UpperGuid}}"]))""");
        result.Should().Be("True,False,True,False,True,True");
    }

    [TestMethod]
    public async Task InListMatchesALetBoundDynamicLiteralAsWritten()
    {
        var result = await ResultAsLines(
            $$"""let L = dynamic(["{{UpperGuid}}", "2015-01-01"]); datatable(s:string)["{{UpperGuid}}", "{{Guid}}", "2015-01-01"] | extend i = s in (L), ni = s !in (L), ic = s in~ (L)""");
        result.Should().Be(string.Join(Environment.NewLine,
            $"{UpperGuid},True,False,True",
            $"{Guid},False,True,True",
            "2015-01-01,True,False,True"));
    }

    [TestMethod]
    public async Task HasAnyMatchesADynamicLiteralAsWritten()
    {
        var result = await ResultAsString(
            $$"""let L = dynamic(["2015-01-01", "{{UpperGuid}}"]); print b = "on 2015-01-01 at" has_any (L), c = "on {{UpperGuid}}" has_any (L), d = "on 2015-01-01T00:00:00.0000000Z" has_any (L)""");
        result.Should().Be("True,True,False");
    }

    [TestMethod]
    public async Task InListOverAParsedValueSeesItsTypedText()
    {
        // parse_json is not a literal: its strings are typed before the comparison
        var result = await ResultAsString(
            """print a = "2015-01-01" in (parse_json('["2015-01-01"]')), b = "2015-01-01T00:00:00.0000000Z" in (parse_json('["2015-01-01"]'))""");
        result.Should().Be("False,True");
    }

    [TestMethod]
    public async Task TypedDynamicOnTheLeftOfInList()
    {
        var result = await ResultAsString(
            """print p = parse_json('{"x":"2015-01-01"}') | project a = p.x in (datetime(2015-01-01)), b = p.x in ("2015-01-01T00:00:00.0000000Z"), c = p.x in ("2015-01-01"), d = p.x in~ ("2015-01-01t00:00:00.0000000z")""");
        result.Should().Be("True,True,False,True");
    }
}
