using System.Text.Json;
using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     ADX types the strings inside a dynamic value it reads from JSON text: a string that parses as
///     a datetime or a guid becomes one.
///     https://learn.microsoft.com/kusto/query/scalar-data-types/dynamic
///     ("Kusto will parse strings as strongly-typed values if they can be parsed as such")
///     Every expected value here was checked against the ADX engine (kustainer). In ADX the typed
///     value reports gettype() = "datetime" / "guid" and its text is the canonical form: a datetime
///     in UTC as yyyy-MM-ddTHH:mm:ss.fffffffZ, a guid in lower case. Strings holding numbers, bools
///     or timespans stay strings (gettype() = "string"), as do strings built by functions.
/// </summary>
[TestClass]
public class DynamicStringTypingTests : TestMethods
{
    private const string Guid = "a0e6d7d4-1c9b-4a1d-9c3e-0f1e2d3c4b5a";
    private const string UpperGuid = "A0E6D7D4-1C9B-4A1D-9C3E-0F1E2D3C4B5A";

    // tostring(parse_json('{"v": <value>}').v): the JSON is written by System.Text.Json and put in a
    // verbatim KQL string, so any character can be tested
    private async Task<string> ParsedValueText(string value)
    {
        var json = JsonSerializer.Serialize(new { v = value }).Replace("'", "''");
        return await ResultAsString($"print s = tostring(parse_json(@'{json}').v)");
    }

    // ADX gettype() = "datetime" for each of these
    [TestMethod]
    [DataRow("2015-01-01", "2015-01-01T00:00:00.0000000Z")]
    [DataRow("2015-01-01T10:20:30Z", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30z", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2015-01-01 10:20:30", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2015-01-01  10:20:30", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2015-01-01\t10:20:30", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2015-01-01\n10:20:30", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20", "2015-01-01T10:20:00.0000000Z")]
    [DataRow("2015-01-01T10:20Z", "2015-01-01T10:20:00.0000000Z")]
    [DataRow("2015-01-01T1:2:3", "2015-01-01T01:02:03.0000000Z")]
    [DataRow("2015-01-01T10:20:30.1234567Z", "2015-01-01T10:20:30.1234567Z")]
    [DataRow("2015-01-01T10:20:30.123Z", "2015-01-01T10:20:30.1230000Z")]
    [DataRow("2015-01-01T10:20:30.5", "2015-01-01T10:20:30.5000000Z")]
    [DataRow("2015-01-01T10:20:30.", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30.99999999Z", "2015-01-01T10:20:30.9999999Z")]
    [DataRow("2015-01-01T10:20:30.123456789+01:00", "2015-01-01T09:20:30.1234567Z")]
    [DataRow("2015-01-01T10:20:30.123 Z", "2015-01-01T10:20:30.1230000Z")]
    [DataRow("2015-01-01T10:20:30+02:00", "2015-01-01T08:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30 +02:00", "2015-01-01T08:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30-0500", "2015-01-01T15:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30+02", "2015-01-01T08:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30+1", "2015-01-01T09:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30+1:2", "2015-01-01T09:18:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30+123", "2014-12-31T22:17:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30+1234", "2014-12-31T21:46:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30-00:00", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30 UTC", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30 utc+1", "2015-01-01T09:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30GMT", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30 GMT+02:00", "2015-01-01T08:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30Z+01:00", "2015-01-01T09:20:30.0000000Z")]
    [DataRow("2015-01-01T10:20:30 \t Z", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2015-01-01T00:00:00+14:00", "2014-12-31T10:00:00.0000000Z")]
    [DataRow("2015-01-01T00:00:00+24:00", "2014-12-31T00:00:00.0000000Z")]
    [DataRow("2015-01-01T00:00:00+05:60", "2014-12-31T18:00:00.0000000Z")]
    [DataRow("2015-12-31T23:59:59-01:00", "2016-01-01T00:59:59.0000000Z")]
    [DataRow("2015-01-01 ", "2015-01-01T00:00:00.0000000Z")]
    [DataRow("2015-01-01\r\n", "2015-01-01T00:00:00.0000000Z")]
    [DataRow("2015-01-01T10:20:30Z ", "2015-01-01T10:20:30.0000000Z")]
    [DataRow("2016-02-29", "2016-02-29T00:00:00.0000000Z")]
    [DataRow("0001-01-01", "0001-01-01T00:00:00.0000000Z")]
    [DataRow("9999-12-31T23:59:59.9999999Z", "9999-12-31T23:59:59.9999999Z")]
    public async Task DateTimeStringIsTyped(string value, string expected)
    {
        (await ParsedValueText(value)).Should().Be(expected);
    }

    // ADX gettype() = "string" for each of these: the value is kept as it is
    [TestMethod]
    [DataRow("2015-01-01T10")]
    [DataRow("2015-01-01T")]
    [DataRow("2015-01-01Z")]
    [DataRow("2015-01-01 GMT")]
    [DataRow("2015-01")]
    [DataRow("2015")]
    [DataRow("20150101")]
    [DataRow("2015-1-1")]
    [DataRow("2015-01-1")]
    [DataRow("02015-01-01")]
    [DataRow("+2015-01-01")]
    [DataRow("15-01-01")]
    [DataRow("2015/01/01")]
    [DataRow("01/02/2015")]
    [DataRow("Jan 1 2015")]
    [DataRow("January 1, 2015")]
    [DataRow("Thu, 01 Jan 2015 00:00:00 GMT")]
    [DataRow("10:20:30")]
    [DataRow("2015-13-01")]
    [DataRow("2015-02-29")]
    [DataRow("2015-01-01T24:00:00")]
    [DataRow("2015-01-01T10:20:60")]
    [DataRow("2015-01-01T100:20:30")]
    [DataRow("2015-01-01T10:20:30:40")]
    [DataRow("2015-01-01T10:20.5")]
    [DataRow("2015-01-01T10:20:30..5")]
    [DataRow("2015-01-01T10:20:30,5Z")]
    [DataRow(" 2015-01-01")]
    [DataRow("2015-01-01x")]
    [DataRow("x2015-01-01")]
    [DataRow("2015-01-01t10:20:30")]
    [DataRow("2015-01-01T 10:20:30")]
    [DataRow("2015-01-01 T10:20:30")]
    [DataRow("2015-01-01 10:20:30")]
    [DataRow("2015-01-01 ")]
    [DataRow("2015-01-01T10:20:30+01:00Z")]
    [DataRow("2015-01-01T10:20:30+12345")]
    [DataRow("2015-01-01T10:20:30+12:345")]
    [DataRow("2015-01-01T10:20:30+123:45")]
    [DataRow("2015-01-01T10:20:30 GMT +01:00")]
    [DataRow("2015-01-01T10:20:30 Z +01:00")]
    [DataRow("2015-01-01T10:20:30 GMTGMT")]
    [DataRow("2015-01-01T10:20:30Z hello")]
    [DataRow("9999-12-31T23:59:59-01:00")]
    [DataRow("0001-01-01T00:00:00+01:00")]
    public async Task StringThatIsNotADateTimeIsKept(string value)
    {
        (await ParsedValueText(value)).Should().Be(value);
    }

    // ADX gettype() = "guid" for each of these
    [TestMethod]
    [DataRow(Guid, Guid)]
    [DataRow(UpperGuid, Guid)]
    [DataRow("A0E6D7D4-1C9B-4A1D-9C3E-0F1E2D3C4B5a", Guid)]
    [DataRow("00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000")]
    public async Task GuidStringIsTyped(string value, string expected)
    {
        (await ParsedValueText(value)).Should().Be(expected);
    }

    // ADX gettype() = "string" for each of these: the value is kept as it is
    [TestMethod]
    [DataRow("{" + Guid + "}")]
    [DataRow("(" + Guid + ")")]
    [DataRow("A0E6D7D41C9B4A1D9C3E0F1E2D3C4B5A")]
    [DataRow("{0xa0e6d7d4,0x1c9b,0x4a1d,{0x9c,0x3e,0x0f,0x1e,0x2d,0x3c,0x4b,0x5a}}")]
    [DataRow(" " + Guid)]
    [DataRow(Guid + " ")]
    [DataRow("a0e6d7d4-1c9b-4a1d-9c3e-0f1e2d3c4b5")]
    [DataRow(Guid + "b")]
    [DataRow("g0e6d7d4-1c9b-4a1d-9c3e-0f1e2d3c4b5a")]
    [DataRow("a0e6d7d4-1c9b4a1d-9c3e-0f1e2d3c4b5a")]
    public async Task StringThatIsNotAGuidIsKept(string value)
    {
        (await ParsedValueText(value)).Should().Be(value);
    }

    // ADX gettype() = "string" for each of these, although the documentation names long and real:
    // a JSON number is a long or a real, but a string holding one stays a string
    [TestMethod]
    [DataRow("123")]
    [DataRow("-123")]
    [DataRow("+5")]
    [DataRow("007")]
    [DataRow("1.5")]
    [DataRow("1e5")]
    [DataRow("9223372036854775808")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("0x10")]
    [DataRow("true")]
    [DataRow("null")]
    [DataRow("01:00:00")]
    [DataRow("1.02:03:04")]
    [DataRow("192.168.0.1")]
    [DataRow("")]
    public async Task NumberBoolAndTimespanStringsAreKept(string value)
    {
        (await ParsedValueText(value)).Should().Be(value);
    }

    [TestMethod]
    public async Task ParseJsonTypesStringsInBagsAndArraysAtAnyDepth()
    {
        // ADX: {"a":"2015-01-01T00:00:00.0000000Z","b":{"c":["a0e6d7d4-...","x"]},"l":"123"}
        var result = await ResultAsString(
            $$"""print p = parse_json('{"a":"2015-01-01","b":{"c":["{{UpperGuid}}","x"]},"l":"123"}') | project s = tostring(p)""");
        result.Should().Be($$"""{"a":"2015-01-01T00:00:00.0000000Z","b":{"c":["{{Guid}}","x"]},"l":"123"}""");
    }

    [TestMethod]
    public async Task ParseJsonOfAJsonStringIsTyped()
    {
        // ADX: parse_json('"2015-01-01"') is a datetime, parse_json('"<guid>"') a guid
        var result = await ResultAsString(
            $$"""print d = tostring(parse_json('"2015-01-01"')), g = tostring(parse_json('"{{UpperGuid}}"'))""");
        result.Should().Be($"2015-01-01T00:00:00.0000000Z,{Guid}");
    }

    [TestMethod]
    public async Task TextThatIsNotJsonIsKeptAsAString()
    {
        // ADX: parse_json('2015-01-01') is not JSON, so it holds the string as it is (gettype() = "string")
        var result = await ResultAsString(
            $"""print a = tostring(parse_json('2015-01-01')), c = tostring(parse_json('{UpperGuid}'))""");
        result.Should().Be($"2015-01-01,{UpperGuid}");
    }

    [TestMethod]
    public async Task ParseJsonOverAColumnTypesEachRow()
    {
        var result = await ResultAsString(
            $$"""datatable(s:string)['{"a":"2015-01-01"}', '{"a":"{{UpperGuid}}"}', '{"a":"2015-01"}'] | project a = tostring(parse_json(s).a)""");
        result.Should().Be($"2015-01-01T00:00:00.0000000Z,{Guid},2015-01");
    }

    [TestMethod]
    public async Task DynamicLiteralsAreTyped()
    {
        // ADX: dynamic({...}) and dynamic([...]) type their strings like parse_json
        var result = await ResultAsString(
            $$"""print b = dynamic({"d":"2015-01-01","g":"{{UpperGuid}}","l":"123"}), a = dynamic(["2015-01-01T10:20:30Z", "x"]) | project d = tostring(b.d), g = tostring(b.g), l = tostring(b.l), a0 = tostring(a[0]), a1 = tostring(a[1])""");
        result.Should().Be($"2015-01-01T00:00:00.0000000Z,{Guid},123,2015-01-01T10:20:30.0000000Z,x");
    }

    [TestMethod]
    public async Task DynamicLiteralsInDatatableAreTyped()
    {
        // ADX: a scalar dynamic literal is typed too - dynamic("2015-01-01") is a datetime
        var result = await ResultAsString(
            $$"""datatable(d:dynamic)[dynamic({"a":"2015-01-01"}), dynamic("2015-01-01 10:20"), dynamic("{{UpperGuid}}"), dynamic("2015-01")] | project s = tostring(d)""");
        result.Should().Be(
            $$"""{"a":"2015-01-01T00:00:00.0000000Z"},2015-01-01T10:20:00.0000000Z,{{Guid}},2015-01""");
    }

    [TestMethod]
    public async Task StringsBuiltByFunctionsAreNotTyped()
    {
        // ADX: bag_pack and split hold the strings they are given (gettype() = "string"), and parse_json
        // of a dynamic value returns it as it is
        var result = await ResultAsString(
            $"""print b = bag_pack("d", "2015-01-01", "g", "{UpperGuid}"), s = split("2015-01-01,x", ",") | project d = tostring(b.d), g = tostring(b.g), s0 = tostring(s[0]), pd = tostring(parse_json(b).d), pg = tostring(parse_json(b).g)""");
        result.Should().Be($"2015-01-01,{UpperGuid},2015-01-01,2015-01-01,{UpperGuid}");
    }

    [TestMethod]
    public async Task PropertyNamesAreNotTyped()
    {
        // ADX: parse_json('{"2015-01-01":1}') keeps the key as it is
        var result = await ResultAsString(
            """print p = parse_json('{"2015-01-01":1}') | project k = tostring(bag_keys(p)[0]), v = tolong(p["2015-01-01"])""");
        result.Should().Be("2015-01-01,1");
    }

    [TestMethod]
    public async Task TypedDateTimeComparesAsItsCanonicalText()
    {
        // ADX: p.x == "2015-01-01" is false, p.x == "2015-01-01T00:00:00.0000000Z" is true,
        // p.x != "2015-01-01" is true, strlen(p.x) is 28 and startswith "2015-01-01" is true
        var result = await ResultAsString(
            """print p = parse_json('{"x":"2015-01-01"}') | project a = p.x == "2015-01-01", b = p.x == "2015-01-01T00:00:00.0000000Z", c = p.x != "2015-01-01", n = strlen(p.x), sw = p.x startswith "2015-01-01", sc = strcat(p.x, "|")""");
        result.Should().Be("False,True,True,28,True,2015-01-01T00:00:00.0000000Z|");
    }

    [TestMethod]
    public async Task TypedDateTimeConvertsToDateTime()
    {
        // ADX: todatetime(p.x) is datetime(2015-01-01 08:20:30.5) and adding a day works
        var result = await ResultAsString(
            """print p = parse_json('{"x":"2015-01-01T10:20:30.5+02:00"}') | project t = todatetime(p.x), e = todatetime(p.x) == datetime(2015-01-01 08:20:30.5), d = todatetime(p.x) + 1d""");
        result.Should().Be("2015-01-01 08:20:30Z,True,2015-01-02 08:20:30Z");
    }

    [TestMethod]
    public async Task TypedGuidComparesAsLowerCaseText()
    {
        // ADX: tostring(p.g) is lower case; p.g == "<UPPER>" is false, p.g == "<lower>" is true,
        // p.g =~ "<UPPER>" is true and toguid(p.g) == toguid("<lower>")
        var result = await ResultAsString(
            $$"""print p = parse_json('{"g":"{{UpperGuid}}"}') | project s = tostring(p.g), u = p.g == "{{UpperGuid}}", l = p.g == "{{Guid}}", t = p.g =~ "{{UpperGuid}}", e = toguid(p.g) == toguid("{{Guid}}")""");
        result.Should().Be($"{Guid},False,True,True,True");
    }

    [TestMethod]
    public async Task NumberStringsKeepTheirTextAndStillConvert()
    {
        // ADX: p.l == "123" is true and tolong(p.l) is 123; p.r == "1.5" and todouble(p.r) is 1.5
        var result = await ResultAsString(
            """print p = parse_json('{"l":"123","r":"1.5"}') | project s = tostring(p.l), e = p.l == "123", l = tolong(p.l), r = todouble(p.r), er = p.r == "1.5" """);
        result.Should().Be("123,True,123,1.5,True");
    }

    [TestMethod]
    public async Task SetHasElementAndArrayIndexOfFindTypedValuesFromTheirText()
    {
        // ADX converts a string value to the type of the element: "2015-01-01" and "2015-01-01 00:00" find the
        // datetime typed from "2015-01-01", either case finds the guid, "Jan 1 2015" (not a datetime to ADX) finds
        // nothing, and an element that stays a string (split) is found by its own text
        var result = await ResultAsString(
            $$"""print p = parse_json('["2015-01-01","2015-01-02"]'), q = parse_json('["{{UpperGuid}}"]'), s = split("2015-01-01,x", ","), m = parse_json('["x","2015-01-01"]') | project a = set_has_element(p, "2015-01-01"), b = set_has_element(p, "2015-01-01 00:00"), c = set_has_element(p, "Jan 1 2015"), d = set_has_element(q, "{{UpperGuid}}"), e = set_has_element(q, "{{Guid}}"), f = array_index_of(p, "2015-01-02"), g = array_index_of(q, "{{Guid}}"), h = set_has_element(s, "2015-01-01"), i = array_index_of(s, "2015-01-01"), k = set_has_element(m, "2015-01-01T00:00:00Z")""");
        result.Should().Be("True,True,False,True,True,1,0,True,0,True");
    }

    [TestMethod]
    public async Task TypedTextRoundTrips()
    {
        // ADX: tostring(p) holds the canonical text, and parse_json of it gives the same values
        var result = await ResultAsString(
            $$"""print p = parse_json('{"x":"2015-01-01T10:20:30+02:00","g":"{{UpperGuid}}"}') | extend b = parse_json(tostring(p)) | project s = tostring(b), x = tostring(b.x) == tostring(p.x), g = tostring(b.g) == tostring(p.g)""");
        result.Should().Be($$"""{"x":"2015-01-01T08:20:30.0000000Z","g":"{{Guid}}"},True,True""");
    }

    [TestMethod]
    public async Task MvExpandKeepsTypedValues()
    {
        var result = await ResultAsString(
            """print p = parse_json('["2015-01-01","x","123"]') | mv-expand e = p | project s = tostring(e)""");
        result.Should().Be("2015-01-01T00:00:00.0000000Z,x,123");
    }
}
