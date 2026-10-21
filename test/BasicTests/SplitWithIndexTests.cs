using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     split(source, delimiter, requestedIndex) returns a string array holding the substring at requestedIndex.
///     https://learn.microsoft.com/kusto/query/split-function
///     Every expected value here was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class SplitWithIndexTests : TestMethods
{
    private const string Table = """
                                 let T = datatable(Id:long, User:string, Props:string) [
                                 1, "alice", '{"role":"admin","level":3,"tags":["a","b"]}',
                                 2, "Bob", '{"role":"user","level":1,"tags":["c"]}',
                                 3, "carol", '{"role":"Admin","level":2,"tags":[]}'
                                 ];
                                 """;

    [TestMethod]
    [DataRow("print a = split('a,b,c', ',', 1)", """["b"]""")]
    [DataRow("print a = split('a,b,c', ',', 2)", """["c"]""")]
    [DataRow("print a = split('a,b,c', ',', 1)[0]", "b")]
    // past the end: null
    [DataRow("print a = split('a,b,c', ',', 3)", "<null>")]
    // constant-folded negative index: -1 is the whole array, below -1 null
    [DataRow("print a = split('a,b,c', ',', -1)", """["a","b","c"]""")]
    [DataRow("print a = split('a,b,c', ',', -2)", "<null>")]
    [DataRow("print a = split('a,b,c', ',', -1)[0]", "a")]
    public async Task Split_Constant(string query, string expected)
    {
        var result = await SquashedLastLineOfResult(query);
        result.Should().Be(expected);
    }

    [TestMethod]
    public async Task Split_PerRow_FirstElement()
    {
        var result = await ResultAsString(Table +
                                          "T | project Id, f1 = split(Props, ',', 0)[0] | where tostring(f1) has 'role' | project Id");
        result.Should().Be("1,2,3");
    }

    [TestMethod]
    public async Task Split_PerRow_NegativeAndOutOfRange()
    {
        // per row, any negative index gives the whole array; past the end gives null
        var result = await ResultAsString(Table +
                                          "T | where Id == 3 | project m1 = split(User, 'r', -1), m2 = split(User, 'r', -2), p = split(User, 'r', 9)");
        Squash(result).Should().Be("""["ca","ol"],["ca","ol"],<null>""");
    }

    [TestMethod]
    public async Task Split_PerRow_ArrayLength()
    {
        var result = await ResultAsString(Table + "T | project Id, n = array_length(split(User, 'l', 1))", ";");
        result.Should().Be("1,1;2,<null>;3,1");
    }
}
