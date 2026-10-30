using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

public static class InOperatorCore
{
    public enum AllAny
    {
        All,
        Any
    }

    public enum EqualsContains
    {
        Equals,
        Contains,

        // whole-term match, as has_any / has_all use: https://learn.microsoft.com/kusto/query/has-anyoperator
        Term
    }

    public static bool CheckString(StringComparison comparison, InContext context, string a, JsonNode jsonArray,
        EqualsContains equals,
        AllAny allAny)
    {
        if (context.LastA == a &&
            context.LastNode == jsonArray)
            return context.LastResult;

        if (jsonArray is JsonArray array)
        {
            context.LastA = a;
            //check the context so that we can avoid repeated work
            if (context.LastNode != jsonArray)
            {
                var stringArray = array.Select(n => n!.ToString())
                    .ToArray();

                context.LastNode = jsonArray;
                context.LastStringArray = stringArray;
            }

            context.LastResult =
                equals == EqualsContains.Term
                    ? allAny == AllAny.Any
                        ? context.LastStringArray.Any(x => TermMatch.Has(a, x, caseSensitive: false))
                        : context.LastStringArray.All(x => TermMatch.Has(a, x, caseSensitive: false))
                    : allAny == AllAny.Any
                    ? equals == EqualsContains.Equals
                        ? context.LastStringArray.Any(x => a.Equals(x, comparison))
                        : context.LastStringArray.Any(x => a.Contains(x, comparison))
                    : equals == EqualsContains.Equals
                        ? context.LastStringArray.All(x => a.Equals(x, comparison))
                        : context.LastStringArray.All(x => a.Contains(x, comparison))
                ;
            return context.LastResult;
        }

        return false;
    }

    public static bool CheckLong(InLongContext context, long a, JsonNode jsonArray)
    {
        if (context.LastA == a &&
            context.LastNode == jsonArray)
            return context.LastResult;

        if (jsonArray is JsonArray array)
        {
            context.LastA = a;
            //check the context so that we can avoid repeated work
            if (context.LastNode != jsonArray)
            {
                var longs = array.Select(n => n!.GetValue<long?>())
                    .ToArray();

                context.LastNode = jsonArray;
                context.LastLongArray = longs;
            }

            context.LastResult = context.LastLongArray.Any(x => a.Equals(x));
            return context.LastResult;
        }

        return false;
    }
}
