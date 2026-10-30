using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kusto.Language.Symbols;
using KustoLoco.Core.DataSource;
using KustoLoco.Core.DataSource.Columns;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

internal class SplitFunctionImpl : IScalarFunctionImpl
{
    public ScalarResult InvokeScalar(ScalarResult[] arguments)
    {
        MyDebug.Assert(arguments.Length == 2);

        return new ScalarResult(ScalarTypes.DynamicArrayOfString,
            Evaluate(arguments[0].Value as string, arguments[1].Value as string));
    }

    public ColumnarResult InvokeColumnar(ColumnarResult[] arguments)
    {
        MyDebug.Assert(arguments.Length == 2);

        var columns = new GenericTypedBaseColumnOfstring[2];
        for (var i = 0; i < arguments.Length; i++)
        {
            columns[i] = (GenericTypedBaseColumnOfstring)arguments[i].Column;
        }

        var rowCount = columns[0].RowCount;
        var data = NullableSetBuilderOfJsonNode.CreateFixed(rowCount);
        for (var i = 0; i < rowCount; i++)
        {
            data[i] = Evaluate(columns[0][i], columns[1][i]);
        }

        return new ColumnarResult(GenericColumnFactoryOfJsonNode.CreateFromDataSet(data.ToNullableSet()));
    }

    protected JsonArray? Evaluate(string? source, string? delimiter)
    {
        if (source is null) return null;
        if (delimiter is null) return null;

        var toks = source.Split(delimiter);
        var n = toks.Select(t => JsonSerializer.SerializeToNode(t)).ToArray();
        return new JsonArray(n);
    }
}

// split(source, delimiter, requestedIndex) returns a string array holding the substring at requestedIndex,
// so split(s, d, i)[0] reads it. https://learn.microsoft.com/kusto/query/split-function
// Out-of-range and negative indexes follow the ADX engine: an index at or past the end gives null; a negative index
// gives the whole array when evaluated per row, while a constant-folded call (every argument constant: the scalar
// path here) gives the whole array for -1 and null below -1.
internal class SplitWithIndexFunctionImpl : IScalarFunctionImpl
{
    public ScalarResult InvokeScalar(ScalarResult[] arguments)
    {
        MyDebug.Assert(arguments.Length == 3);

        var result = Evaluate(arguments[0].Value as string, arguments[1].Value as string, GetLongValue(arguments[2].Value),
            constantFolded: true);
        return new ScalarResult(ScalarTypes.Dynamic, result);
    }

    public ColumnarResult InvokeColumnar(ColumnarResult[] arguments)
    {
        MyDebug.Assert(arguments.Length == 3);

        var sourceColumn = (GenericTypedBaseColumnOfstring)arguments[0].Column;
        var delimiterColumn = (GenericTypedBaseColumnOfstring)arguments[1].Column;
        var indexColumn = (GenericTypedBaseColumnOflong)arguments[2].Column;

        var rowCount = sourceColumn.RowCount;
        var data = NullableSetBuilderOfJsonNode.CreateFixed(rowCount);
        for (var i = 0; i < rowCount; i++)
        {
            data[i] = Evaluate(sourceColumn[i], delimiterColumn[i], indexColumn[i], constantFolded: false);
        }

        return new ColumnarResult(GenericColumnFactoryOfJsonNode.CreateFromDataSet(data.ToNullableSet()));
    }

    private static long? GetLongValue(object? value)
    {
        return value switch
        {
            long l => l,
            int i => i,
            JsonValue jv when jv.TryGetValue<long>(out var lv) => lv,
            JsonValue jv when jv.TryGetValue<int>(out var iv) => iv,
            _ => null
        };
    }

    private static JsonArray? Evaluate(string? source, string? delimiter, long? requestedIndex, bool constantFolded)
    {
        if (source is null) return null;
        if (delimiter is null) return null;
        if (requestedIndex is null) return null;

        var toks = source.Split(delimiter);
        var indexValue = requestedIndex.Value;

        if (indexValue < 0)
        {
            return constantFolded && indexValue < -1
                ? null
                : new JsonArray(toks.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray());
        }

        if (indexValue >= toks.Length)
        {
            return null;
        }

        return new JsonArray(JsonValue.Create(toks[(int)indexValue]));
    }
}
