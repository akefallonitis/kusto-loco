using System.Diagnostics;
using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     Kusto evaluates every regular expression with RE2, a linear-time engine with no backtracking:
///     https://learn.microsoft.com/kusto/query/re2. The regex builtins ran .NET's backtracking engine, so a
///     pattern such as <c>^(.+)@[^@]+$</c> (an address split a detection rule uses) was quadratic in a field with no
///     match — a single long attacker-controlled value could stall a whole query — and a pattern RE2 rejects
///     (a lookaround, a backreference) silently matched. Every builtin now compiles its pattern once, non-backtracking:
///     the same leftmost matches and captures, linear cost, and RE2's construct set.
/// </summary>
[TestClass]
public class RegexLinearCostTests : TestMethods
{
    // 100 000 characters, '@' everywhere and a trailing '@' (strrep caps at 1024 repeats, as in Kusto, so it nests):
    // the required literal is present, so no literal prefilter short-cuts the scan, and `(.+)@[^@]+$` can never hold
    // — a backtracking engine retries every '@' from every start position (≈ n²/4 steps); RE2 is one pass.
    private const string Long = "strrep(strrep('x@', 1000), 50)";
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(2);

    [TestMethod]
    [Timeout(30_000)]
    [DataRow($"print r = {Long} matches regex @'(.+)@[^@]+$'", "False")]
    [DataRow($"print r = extract(@'(.+)@[^@]+$', 1, {Long})", "")]
    [DataRow($"print r = array_length(extract_all(@'(.+)@[^@]+$', {Long}))", "0")]
    [DataRow($"print r = countof({Long}, @'(.+)@[^@]+$', 'regex')", "0")]
    [DataRow($"print r = strlen(trim_end(@'(.+)@[^@]+', {Long}))", "100000")]
    public async Task A_regex_builtin_is_linear_in_an_unmatched_long_field(string query, string expected)
    {
        var watch = Stopwatch.StartNew();
        var result = await LastLineOfResult(query);
        watch.Stop();
        result.Should().Be(expected);
        watch.Elapsed.Should().BeLessThan(Bound, "RE2 semantics are linear-time; backtracking is quadratic here");
    }

    [TestMethod]
    [DataRow("print 'ab' matches regex @'a(?=b)'")]          // lookahead
    [DataRow("print extract(@'(a)\\1', 1, 'aa')")]          // backreference
    public async Task A_construct_RE2_rejects_is_an_error_naming_RE2(string query)
    {
        var result = await CreateContext().RunQuery(query);
        result.Error.Should().Contain("RE2");
    }

    [TestMethod]
    // the shipped rules' own shapes keep their captures
    [DataRow("print extract(@'^(.+)@[^@]+$', 1, 'first.last@contoso.com')", "first.last")]
    [DataRow("print extract(@'@([^@]+)$', 1, 'first.last@contoso.com')", "contoso.com")]
    [DataRow("print extract(@'https?://([^/]+)', 1, 'see https://evil.example/x')", "evil.example")]
    [DataRow("print extract(@'_([^_]+)#ext#', 1, 'guest_fabrikam.com#ext#@contoso.onmicrosoft.com')", "fabrikam.com")]
    [DataRow("print 'Set-Mailbox' matches regex @'^(Set|New)-'", "True")]
    [DataRow("print countof('a1b22c333', @'\\d+', 'regex')", "3")]
    [DataRow("print trim(@'\\s+', '  x  ')", "x")]
    public async Task Leftmost_matches_and_captures_are_unchanged(string query, string expected)
    {
        (await LastLineOfResult(query)).Should().Be(expected);
    }
}
