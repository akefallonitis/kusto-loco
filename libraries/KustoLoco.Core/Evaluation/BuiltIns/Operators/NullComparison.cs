//
// Licensed under the MIT License.

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

/// <summary>
///     == and != when an operand is null, as in ADX (https://learn.microsoft.com/kusto/query/null-values): with one
///     null operand == is false and != is true; with two the result is null. A separate class because the source
///     generator copies each Impl method into a class of its own.
/// </summary>
internal static class NullComparison
{
    public static bool? Equal(bool leftHasValue, bool rightHasValue) => leftHasValue || rightHasValue ? false : null;

    public static bool? NotEqual(bool leftHasValue, bool rightHasValue) => leftHasValue || rightHasValue ? true : null;
}
