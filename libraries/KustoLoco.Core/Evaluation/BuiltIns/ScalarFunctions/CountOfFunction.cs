using System.Text.RegularExpressions;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

[KustoImplementation(Keyword = "Functions.CountOf")]
internal partial class CountOfFunction
{

    public static long Do(string source, string substring, string kind) =>
        kind == "regex"
            ? KqlRegex.Get(substring).Count(source)
            : KqlRegex.Get(Regex.Escape(substring)).Count(source);

    public long Impl(string source, string substring) => CountOfFunction.Do(source, substring, "normal");

    public long RImpl(string source, string substring, string kind) => CountOfFunction.Do(source, substring, kind);
}
