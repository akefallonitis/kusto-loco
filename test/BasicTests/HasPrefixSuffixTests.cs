using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     hasprefix / hassuffix, their case-sensitive forms and negations.
///     https://learn.microsoft.com/kusto/query/hasprefix-operator
///     https://learn.microsoft.com/kusto/query/hassuffix-operator
///     Every expected value here was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class HasPrefixSuffixTests : TestMethods
{
    private const string Table = """
                                 let T = datatable(Id:long, S:string) [
                                 1, "North America",
                                 2, "Nordic Region",
                                 3, @"C:\Windows\System32\CMD.exe",
                                 4, "admin@Contoso.com"
                                 ];
                                 """;

    [TestMethod]
    [DataRow("North America", "hasprefix", "ame", true)] // the documentation's own example
    [DataRow("North America", "hasprefix", "mer", false)]
    [DataRow("North America", "hassuffix", "ica", true)]
    [DataRow("North America", "hassuffix", "eri", false)]
    [DataRow("svc_backup01", "hasprefix", "backup", true)] // '_' separates terms
    [DataRow(@"C:\Windows\System32\cmd.exe", "hassuffix", ".exe", true)]
    [DataRow(@"C:\Windows\System32\cmd.exe", "hasprefix", "cmd.", true)]
    [DataRow(@"C:\Windows\System32\cmd.exe", "hasprefix", "md.", false)]
    public async Task TermRule(string text, string op, string needle, bool expected)
    {
        var result = await LastLineOfResult($"print r = @\"{text}\" {op} \"{needle}\"");
        result.Should().Be(expected.ToString());
    }

    [TestMethod]
    [DataRow("T | where S hasprefix 'nor' | project Id", "1,2")]
    [DataRow("T | where S hasprefix 'ame' | project Id", "1")]
    [DataRow("T | where S hasprefix 'mer' | project Id", "")]
    [DataRow("T | where S hasprefix_cs 'Nor' | project Id", "1,2")]
    [DataRow("T | where S hasprefix_cs 'nor' | project Id", "")]
    [DataRow("T | where S !hasprefix 'con' | project Id", "1,2,3")]
    [DataRow("T | where S !hasprefix_cs 'Con' | project Id", "1,2,3")]
    [DataRow("T | where S hassuffix 'ica' | project Id", "1")]
    [DataRow("T | where S hassuffix 'exe' | project Id", "3")]
    [DataRow("T | where S hassuffix_cs 'MD' | project Id", "3")]
    [DataRow("T | where S hassuffix_cs 'md' | project Id", "")]
    [DataRow("T | where S !hassuffix 'oso' | project Id", "1,2,3")]
    [DataRow("T | where S !hassuffix_cs 'OSO' | project Id", "1,2,3,4")]
    public async Task Operators(string query, string expected)
    {
        var result = await ResultAsString(Table + query);
        result.Should().Be(expected);
    }
}
