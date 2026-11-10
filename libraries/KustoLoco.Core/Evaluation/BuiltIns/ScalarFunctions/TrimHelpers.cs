using System.Text.RegularExpressions;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

public static class TrimHelpers
{
    public static  string Evaluate(string pattern,string target, bool trimStart, bool trimEnd)
    {
        if (trimStart) target = KqlRegex.Get("^" + pattern).Replace(target, string.Empty);
        if (trimEnd) target = KqlRegex.Get(pattern + "$").Replace(target, string.Empty);
        return target;
    }
}
