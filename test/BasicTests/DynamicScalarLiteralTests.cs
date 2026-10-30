using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     A dynamic literal that holds a scalar - dynamic("x"), dynamic(5), dynamic(true) - is a
///     dynamic value like any other, so it can be put in a column and passed to functions.
///     https://learn.microsoft.com/kusto/query/scalar-data-types/dynamic
///     https://learn.microsoft.com/kusto/query/parse-json-function
///     Every expected value here was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class DynamicScalarLiteralTests : TestMethods
{
    [TestMethod]
    public async Task ParseJsonOfADynamicStringReturnsItUnchanged()
    {
        // parse_json of a dynamic value returns it as is: the string is not parsed again,
        // so it has no property 'a'
        var result = await ResultAsString(
            """print d = dynamic("{\"a\":1}") | extend p = parse_json(d) | extend a = p.a""");
        result.Should().Be("""{"a":1},{"a":1},<null>""");
    }

    [TestMethod]
    public async Task ParseJsonOfTheStringFormParsesIt()
    {
        var result = await ResultAsString(
            """print d = dynamic("{\"a\":1}") | extend p = parse_json(tostring(d)) | project a = p.a""");
        result.Should().Be("1");
    }

    [TestMethod]
    public async Task DynamicStringInAColumnWorksAsInDatatable()
    {
        var result = await ResultAsString(
            """datatable(d:dynamic)[dynamic("{\"a\":1}"), dynamic({"a":2})] | extend p = parse_json(d) | project a = p.a""");
        result.Should().Be("<null>,2");
    }

    [TestMethod]
    public async Task ScalarDynamicLiteralsCanBePrinted()
    {
        var result = await ResultAsString(
            """print d = dynamic(5), s = dynamic("x"), b = dynamic(true), r = dynamic(1.5) | project n = tolong(d) * 2, l = strlen(tostring(s)), x = iff(tobool(b), "y", "n"), rr = todouble(r) * 2""");
        result.Should().Be("10,1,y,3");
    }

    [TestMethod]
    public async Task DynamicStringComparesAndConcatenatesAsAString()
    {
        var result = await ResultAsString(
            """print d = dynamic("x") | project e = isempty(d), n = isnull(d), c = d == "x", s = strcat(d, "b")""");
        result.Should().Be("False,False,True,xb");
    }

    [TestMethod]
    public async Task DynamicStringFromLet()
    {
        var result = await ResultAsString("""let v = dynamic("a"); print v, x = strcat(v, "b")""");
        result.Should().Be("a,ab");
    }
}
