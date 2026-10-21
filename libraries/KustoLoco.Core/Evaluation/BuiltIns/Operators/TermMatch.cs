using System;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

/// <summary>
///     The term rule behind the has family of string operators.
/// </summary>
/// <remarks>
///     ADX breaks a string into terms, each a maximal run of alphanumeric characters
///     (https://learn.microsoft.com/kusto/query/datatypes-string-operators#what-is-a-term). Alphanumeric means
///     Unicode letters and digits, not only ASCII: "naïve" has "na" and "приветмир" has "мир" are both false,
///     while '_' and punctuation separate terms ("a_b" has "a" is true).
///     <c>has</c> matches when the needle occurs aligned to term boundaries: when the needle starts with a term
///     character the text must have none immediately before the occurrence, and when it ends with one the text
///     must have none immediately after. <c>hasprefix</c> keeps only the leading condition and <c>hassuffix</c>
///     only the trailing one (https://learn.microsoft.com/kusto/query/hasprefix-operator,
///     https://learn.microsoft.com/kusto/query/hassuffix-operator).
///     Case-insensitive matching is simple ordinal folding (É matches é; ß does not match SS, İ does not match i),
///     and an empty needle matches, as ADX gives both.
/// </remarks>
public static class TermMatch
{
    public enum Kind
    {
        Has,
        Prefix,
        Suffix
    }

    public static bool Has(string text, string needle, bool caseSensitive) =>
        Match(text, needle, Kind.Has, caseSensitive);

    public static bool Match(string text, string needle, Kind kind, bool caseSensitive)
    {
        if (needle.Length == 0) return true;
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var checkBefore = kind != Kind.Suffix && IsTermChar(needle[0]);
        var checkAfter = kind != Kind.Prefix && IsTermChar(needle[^1]);
        var start = 0;
        while (start <= text.Length - needle.Length)
        {
            var at = text.IndexOf(needle, start, comparison);
            if (at < 0) return false;
            var end = at + needle.Length;
            if ((!checkBefore || at == 0 || !IsTermChar(text[at - 1]))
                && (!checkAfter || end == text.Length || !IsTermChar(text[end])))
                return true;
            start = at + 1;
        }

        return false;
    }

    public static bool IsTermChar(char c) => char.IsLetterOrDigit(c);
}
