//
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Kusto.Language.Symbols;
using KustoLoco.Core.DataSource;
using KustoLoco.Core.DataSource.Columns;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

/// <summary>
///     Collects the values of a dynamic column the way make_set, make_list and their _if and
///     _with_nulls forms do in ADX: a dynamic array contributes its elements rather than itself.
/// </summary>
/// <remarks>
///     https://learn.microsoft.com/kusto/query/make-set-aggregation-function
///     https://learn.microsoft.com/kusto/query/make-list-aggregation-function
///     Checked against the ADX engine (kustainer):
///     - an array is flattened one level: [1, [2, 3]] contributes 1 and [2, 3];
///     - make_set / make_list skip a null value and a null array element; [] contributes nothing;
///     - make_list_with_nulls keeps both as null;
///     - make_set compares values by their JSON text, so 1 and 1.0, "1" and 1, and two bags
///       with the same properties in a different order are all distinct;
///     - the size limit counts elements: make_list(d, 1) over ["a", "b"] is ["a"].
/// </remarks>
internal static class DynamicValueCollector
{
    public static JsonArray Collect(GenericTypedBaseColumnOfJsonNode values,
        GenericTypedBaseColumnOfbool? predicates,
        long maxSize,
        bool distinct)
    {
        var result = new List<JsonNode?>();
        var seen = distinct ? new HashSet<string>() : null;

        for (var i = 0; i < values.RowCount; i++)
        {
            if (predicates != null && predicates[i] != true) continue;

            var value = values[i];
            if (value == null) continue;

            if (value is JsonArray array)
            {
                foreach (var element in array)
                {
                    if (element != null && !Add(element))
                        return JsonArrayHelper.From(result);
                }
            }
            else if (!Add(value))
            {
                return JsonArrayHelper.From(result);
            }
        }

        return JsonArrayHelper.From(result);

        // returns false once the size limit is reached
        bool Add(JsonNode node)
        {
            if (seen != null && !seen.Add(node.ToJsonString()))
                return true;
            // the node may already belong to the source value
            result.Add(node.DeepClone());
            return result.Count < maxSize;
        }
    }

    public static JsonArray CollectWithNulls(GenericTypedBaseColumnOfJsonNode values)
    {
        var result = new List<JsonNode?>();
        for (var i = 0; i < values.RowCount; i++)
        {
            var value = values[i];
            if (value is JsonArray array)
            {
                foreach (var element in array)
                    result.Add(element?.DeepClone());
            }
            else
            {
                result.Add(value?.DeepClone());
            }
        }

        return JsonArrayHelper.From(result);
    }

    public static long MaxSize(ColumnarResult[] arguments, int index)
    {
        if (arguments.Length <= index) return long.MaxValue;
        var maxSizeColumn = (GenericTypedBaseColumnOflong)arguments[index].Column;
        return maxSizeColumn.RowCount > 0
            ? maxSizeColumn[0] ?? long.MaxValue
            : long.MaxValue;
    }
}

internal class MakeSetDynamicFunctionImpl : IAggregateImpl
{
    public EvaluationResult Invoke(ITableChunk chunk, ColumnarResult[] arguments)
    {
        MyDebug.Assert(arguments.Length == 1 || arguments.Length == 2);
        var valuesColumn = (GenericTypedBaseColumnOfJsonNode)arguments[0].Column;
        var set = DynamicValueCollector.Collect(valuesColumn, null,
            DynamicValueCollector.MaxSize(arguments, 1), true);
        return new ScalarResult(ScalarTypes.Dynamic, set);
    }
}

internal class MakeSetIfDynamicFunctionImpl : IAggregateImpl
{
    public EvaluationResult Invoke(ITableChunk chunk, ColumnarResult[] arguments)
    {
        MyDebug.Assert(arguments.Length == 2 || arguments.Length == 3);
        var valuesColumn = (GenericTypedBaseColumnOfJsonNode)arguments[0].Column;
        var predicatesColumn = (GenericTypedBaseColumnOfbool)arguments[1].Column;
        MyDebug.Assert(valuesColumn.RowCount == predicatesColumn.RowCount);
        var set = DynamicValueCollector.Collect(valuesColumn, predicatesColumn,
            DynamicValueCollector.MaxSize(arguments, 2), true);
        return new ScalarResult(ScalarTypes.Dynamic, set);
    }
}

internal class MakeListWithNullsDynamicFunctionImpl : IAggregateImpl
{
    public EvaluationResult Invoke(ITableChunk chunk, ColumnarResult[] arguments)
    {
        MyDebug.Assert(arguments.Length == 1);
        var valuesColumn = (GenericTypedBaseColumnOfJsonNode)arguments[0].Column;
        return new ScalarResult(ScalarTypes.Dynamic, DynamicValueCollector.CollectWithNulls(valuesColumn));
    }
}
