using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

/// <summary>
///     The one construction of a KQL regular expression, for every regex builtin (<c>matches regex</c>, <c>extract</c>,
///     <c>extract_all</c>, <c>countof</c> with kind <c>regex</c>, the <c>trim</c> family).
///     Kusto evaluates regular expressions with RE2 (https://learn.microsoft.com/kusto/query/re2): linear time, no
///     backtracking, no lookarounds or backreferences. <see cref="RegexOptions.NonBacktracking"/> is .NET's engine with
///     the same guarantees and the same construct set, finding the same leftmost match and captures, so a pattern costs
///     linear time in its input — one long field can no longer stall a query — and a construct RE2 rejects is rejected
///     here too (an <see cref="ArgumentException"/> naming RE2, which each builtin treats as it treats a bad pattern).
///     A pattern is compiled once: the static <c>Regex.*</c> methods cache only 15 patterns, fewer than a rule set uses,
///     so patterns were re-built per call once the set outgrew it. The cache is bounded (patterns can come from data).
/// </summary>
internal static class KqlRegex
{
    internal const int MaxCachedPatterns = 1024;
    private static readonly ConcurrentDictionary<string, Regex> Cache = new(StringComparer.Ordinal);

    /// <summary>The compiled pattern; an <see cref="ArgumentException"/> when it is not a valid RE2 pattern.</summary>
    internal static Regex Get(string pattern)
    {
        if (Cache.TryGetValue(pattern, out var cached)) return cached;
        var rx = Create(pattern);
        if (Cache.Count >= MaxCachedPatterns) Cache.Clear();
        Cache.TryAdd(pattern, rx);
        return rx;
    }

    private static Regex Create(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        }
        catch (NotSupportedException e)
        {
            throw new ArgumentException(
                $"regex '{pattern}' uses a construct RE2 does not support (Kusto evaluates regular expressions with RE2): {e.Message}",
                nameof(pattern), e);
        }
    }
}
