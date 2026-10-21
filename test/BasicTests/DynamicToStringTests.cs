using System.Text.Json.Nodes;
using AwesomeAssertions;
using KustoLoco.Core;

namespace BasicTests;

/// <summary>
///     A dynamic value converted to a string is compact JSON in which only the characters JSON
///     requires are escaped: the quote, the backslash and control characters.
///     https://learn.microsoft.com/kusto/query/tostring-function
///     Every expected value here was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class DynamicToStringTests : TestMethods
{
    private async Task<string> Single(string query)
    {
        var result = await Result(query);
        return (string)result.Get(0, 0)!;
    }

    [TestMethod]
    public async Task HtmlSensitiveCharactersAreNotEscaped()
    {
        var s = await Single("""print s = tostring(dynamic({"a":"x>y","b":"q\"r","c":"<&'"}))""");
        s.Should().Be("""{"a":"x>y","b":"q\"r","c":"<&'"}""");
    }

    [TestMethod]
    public async Task OnlyQuoteBackslashAndControlCharactersAreEscaped()
    {
        // U+007F, U+0085, U+00A0, U+00AD, U+2028, U+2029, an emoji and U+FEFF are written as they
        // are; the query spells them as JSON escapes inside the dynamic literal
        const string query =
            @"print s = tostring(dynamic({""a"":""\u0001\u001f\u007f\u0085\u00a0\u00ad\u2028\u2029\ud83d\ude00\ufeff\\/+`""}))";
        var s = await Single(query);
        s.Should().Be("{\"a\":\"\\u0001\\u001F\u007f\u0085\u00a0\u00ad\u2028\u2029\ud83d\ude00\ufeff\\\\/+`\"}");
    }

    [TestMethod]
    public async Task ShortEscapesForCommonControlCharacters()
    {
        var s = await Single("""print s = tostring(dynamic(["\b\f\n\r\t"]))""");
        s.Should().Be("""["\b\f\n\r\t"]""");
    }

    [TestMethod]
    public async Task NonAsciiIsNotEscaped()
    {
        var s = await Single("""print s = tostring(dynamic(["<&>", "é日", "\t\n"]))""");
        s.Should().Be("""["<&>","é日","\t\n"]""");
    }

    [TestMethod]
    public async Task EscapesInTheSourceJsonAreNotKept()
    {
        var s = await Single("""print s = tostring(parse_json('{"x":"\\u00e9 é \\u003c"}'))""");
        s.Should().Be("""{"x":"é é <"}""");
    }

    [TestMethod]
    public async Task BagPackAndMakeListValues()
    {
        (await Single("""print s = tostring(bag_pack("k", "<&>'\""))"""))
            .Should().Be("""{"k":"<&>'\""}""");
        (await Single("""datatable(x:string)["<&>","é"] | summarize l = make_list(x) | project s = tostring(l)"""))
            .Should().Be("""["<&>","é"]""");
    }

    [TestMethod]
    public async Task StrcatConvertsDynamicToCompactJson()
    {
        var s = await Single("""print s = strcat(dynamic({"a":"<"}), dynamic([1,"é"]))""");
        s.Should().Be("""{"a":"<"}[1,"é"]""");
    }

    [TestMethod]
    public async Task StrcatArrayWritesElementsAsTostringDoes()
    {
        var s = await Single("""print f = strcat_array(dynamic([{"a":"<"},"<",null,1]), ",")""");
        s.Should().Be("""{"a":"<"},<,,1""");
    }

    [TestMethod]
    public async Task MvExpandToStringWritesCompactJson()
    {
        var result = await ResultAsString(
            """print d = dynamic([{"a":"<&>"}, "é", 1]) | mv-expand x = d to typeof(string) | project x""",
            "|");
        result.Should().Be("""{"a":"<&>"}|é|1""");
    }

    [TestMethod]
    public async Task StringValuesAndNull()
    {
        var result = await ResultAsString(
            """print s = tostring(dynamic([{"k":"<"}, "x>y"])), n = tostring(dynamic(null)), q = tostring(dynamic({"a":"a\"b"}).a)""");
        result.Should().Be("""[{"k":"<"},"x>y"],,a"b""");
    }

    [TestMethod]
    public void DisplayKeepsIndentationWithoutEscaping()
    {
        var node = JsonNode.Parse("""{"a":"<&>é"}""")!;
        KustoFormatter.ObjectToKustoString(node)
            .ReplaceLineEndings("\n")
            .Should().Be("{\n  \"a\": \"<&>é\"\n}");
    }
}
