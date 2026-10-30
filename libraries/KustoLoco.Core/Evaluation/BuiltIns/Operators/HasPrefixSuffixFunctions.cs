//
// Licensed under the MIT License.

// hasprefix / hassuffix, their case-sensitive forms and negations.
// A match is a needle occurrence aligned to a term boundary at its start (hasprefix) or at its end (hassuffix);
// see TermMatch for the term rule.
// https://learn.microsoft.com/kusto/query/hasprefix-operator
// https://learn.microsoft.com/kusto/query/hassuffix-operator

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

[KustoImplementation(Keyword = "Operators.HasPrefix")]
internal partial class HasPrefixFunction
{
    private static bool Impl(string a, string b) => TermMatch.Match(a, b, TermMatch.Kind.Prefix, caseSensitive: false);
}

[KustoImplementation(Keyword = "Operators.HasPrefixCs")]
internal partial class HasPrefixCsFunction
{
    private static bool Impl(string a, string b) => TermMatch.Match(a, b, TermMatch.Kind.Prefix, caseSensitive: true);
}

[KustoImplementation(Keyword = "Operators.NotHasPrefix")]
internal partial class NotHasPrefixFunction
{
    private static bool Impl(string a, string b) => !TermMatch.Match(a, b, TermMatch.Kind.Prefix, caseSensitive: false);
}

[KustoImplementation(Keyword = "Operators.NotHasPrefixCs")]
internal partial class NotHasPrefixCsFunction
{
    private static bool Impl(string a, string b) => !TermMatch.Match(a, b, TermMatch.Kind.Prefix, caseSensitive: true);
}

[KustoImplementation(Keyword = "Operators.HasSuffix")]
internal partial class HasSuffixFunction
{
    private static bool Impl(string a, string b) => TermMatch.Match(a, b, TermMatch.Kind.Suffix, caseSensitive: false);
}

[KustoImplementation(Keyword = "Operators.HasSuffixCs")]
internal partial class HasSuffixCsFunction
{
    private static bool Impl(string a, string b) => TermMatch.Match(a, b, TermMatch.Kind.Suffix, caseSensitive: true);
}

[KustoImplementation(Keyword = "Operators.NotHasSuffix")]
internal partial class NotHasSuffixFunction
{
    private static bool Impl(string a, string b) => !TermMatch.Match(a, b, TermMatch.Kind.Suffix, caseSensitive: false);
}

[KustoImplementation(Keyword = "Operators.NotHasSuffixCs")]
internal partial class NotHasSuffixCsFunction
{
    private static bool Impl(string a, string b) => !TermMatch.Match(a, b, TermMatch.Kind.Suffix, caseSensitive: true);
}
