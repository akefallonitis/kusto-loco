using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     has, !has, has_cs, !has_cs, has_any and has_all match whole terms, not substrings.
///     https://learn.microsoft.com/kusto/query/datatypes-string-operators#what-is-a-term
///     Every expected value here was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class HasTermTests : TestMethods
{
    private const string Table = """
                                 let T = datatable(Id:long, S:string) [
                                 1, "Invoke-Mimikatz -Command",
                                 2, "mimikatzlike",
                                 3, "user@contoso.com",
                                 4, @"CONTOSO\Admin",
                                 5, "powershell.exe -enc AAAA"
                                 ];
                                 """;

    private static string Kql(string s) => "@\"" + s.Replace("\"", "\"\"") + "\"";

    [TestMethod]
    [DataRow("Invoke-Mimikatz -Command", "mimikatz", true)]
    [DataRow("mimikatzlike", "mimikatz", false)]
    [DataRow("xmimikatz", "mimikatz", false)]
    [DataRow("user@contoso.com", "contoso", true)]
    [DataRow("user@contoso.com", "contoso.com", true)]
    [DataRow("user@contoso.com", "toso.com", false)]
    [DataRow("user@contoso.com", "@contoso", true)] // starts with a delimiter: no leading boundary needed
    [DataRow("user@contoso.com", "r@contoso", false)]
    [DataRow(@"CONTOSO\admin", "admin", true)]
    [DataRow("abc abcd", "abc", true)] // an aligned occurrence after an unaligned one
    [DataRow("abcd abc", "abc", true)]
    [DataRow("a_b", "a", true)] // '_' is not alphanumeric, so it separates terms
    [DataRow("a_b", "b", true)]
    [DataRow("x_y", "x_y", true)]
    [DataRow("AAA", "aA", false)]
    // non-ASCII letters and digits are term characters
    [DataRow("naïve x", "na", false)]
    [DataRow("привет мир", "мир", true)]
    [DataRow("приветмир", "мир", false)]
    [DataRow("abcédef", "abc", false)]
    [DataRow("abcédef", "def", false)]
    [DataRow("über", "ber", false)]
    [DataRow("日本語テキスト", "本", false)]
    // case folding is simple ordinal folding: no expansions, no Turkish dotted I
    [DataRow("ÉCOLE", "école", true)]
    [DataRow("Straße", "STRASSE", false)]
    [DataRow("İstanbul", "istanbul", false)]
    // an empty needle matches
    [DataRow("x", "", true)]
    public async Task Has_MatchesWholeTerms(string text, string needle, bool expected)
    {
        var result = await LastLineOfResult($"print r = {Kql(text)} has {Kql(needle)}");
        result.Should().Be(expected.ToString());
        var negated = await LastLineOfResult($"print r = {Kql(text)} !has {Kql(needle)}");
        negated.Should().Be((!expected).ToString());
    }

    [TestMethod]
    [DataRow(@"CONTOSO\admin", "Admin", false)]
    [DataRow(@"CONTOSO\admin", "admin", true)]
    [DataRow("ab", "AB", false)]
    public async Task HasCs_MatchesWholeTerms(string text, string needle, bool expected)
    {
        var result = await LastLineOfResult($"print r = {Kql(text)} has_cs {Kql(needle)}");
        result.Should().Be(expected.ToString());
        var negated = await LastLineOfResult($"print r = {Kql(text)} !has_cs {Kql(needle)}");
        negated.Should().Be((!expected).ToString());
    }

    [TestMethod]
    [DataRow("T | where S has 'mimikatz' | project Id", "1")]
    [DataRow("T | where S !has 'mimikatz' | project Id", "2,3,4,5")]
    [DataRow("T | where S has_cs 'Admin' | project Id", "4")]
    [DataRow("T | where S has_cs 'admin' | project Id", "")]
    [DataRow("T | where S !has_cs 'Mimikatz' | project Id", "2,3,4,5")]
    [DataRow("T | where S has 'contoso' | project Id", "3,4")]
    [DataRow("T | where S has 'toso' | project Id", "")]
    [DataRow("T | where S has_any ('mimikatz', 'enc') | project Id", "1,5")]
    [DataRow("T | where S has_any ('katz', 'shell') | project Id", "")]
    [DataRow("T | where S has_all ('powershell', 'enc') | project Id", "5")]
    [DataRow("T | where S has_all ('power', 'enc') | project Id", "")]
    public async Task HasFamily_FiltersOnWholeTerms(string query, string expected)
    {
        var result = await ResultAsString(Table + query);
        result.Should().Be(expected);
    }
}
