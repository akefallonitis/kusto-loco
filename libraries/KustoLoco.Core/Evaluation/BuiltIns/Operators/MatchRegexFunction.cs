using System.Text.RegularExpressions;

// ReSharper disable PartialTypeWithSinglePart
namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

/// <remarks>RE2 semantics through <see cref="KqlRegex"/>: linear time, RE2's construct set.</remarks>
[KustoImplementation(Keyword = "Operators.MatchRegex")]
internal partial class MatchRegexFunction
{
    internal static bool Impl(string str, string pattern) => KqlRegex.Get(pattern).IsMatch(str);
}
