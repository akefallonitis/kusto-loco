//
// Licensed under the MIT License.

// Evaluation of IRInListNode: list membership with ADX semantics.
// https://learn.microsoft.com/kusto/query/in-operator , https://learn.microsoft.com/kusto/query/in-cs-operator

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kusto.Language.Symbols;
using KustoLoco.Core.DataSource;
using KustoLoco.Core.DataSource.Columns;
using KustoLoco.Core.InternalRepresentation.Nodes.Expressions;
using KustoLoco.Core.Util;
using KustoLoco.Core.Extensions;

namespace KustoLoco.Core.Evaluation;

internal partial class TreeEvaluator
{
    public override EvaluationResult VisitInList(IRInListNode node, EvaluationContext context)
    {
        var comparer = node.CaseInsensitive && node.Domain == InListDomain.String
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var fixedKeys = new HashSet<string>(comparer);
        var perRow = new List<BaseColumn>();

        foreach (var element in node.Elements)
        {
            switch (element.Accept(this, context))
            {
                case TabularResult table:
                    foreach (var chunk in table.Value.GetData())
                    {
                        if (chunk.Columns.Length == 0) continue;
                        var column = chunk.Columns[0];
                        for (var i = 0; i < chunk.RowCount; i++)
                            AddKeys(fixedKeys, column.GetRawDataValue(i), node.Domain);
                    }
                    break;
                case ScalarResult scalar:
                    AddKeys(fixedKeys, scalar.Value, node.Domain);
                    break;
                case ColumnarResult columnar:
                    perRow.Add(columnar.Column);
                    break;
            }
        }

        var left = node.Left.Accept(this, context);
        if (node.ResultKind == EvaluatedExpressionKind.Scalar)
        {
            var value = left is ScalarResult s ? s.Value : null;
            return new ScalarResult(ScalarTypes.Bool, Test(node, value, fixedKeys, perRow, 0, comparer));
        }

        var leftColumn = left is ColumnarResult lc ? lc.Column : null;
        var leftScalar = left is ScalarResult ls ? ls.Value : null;
        var rowCount = leftColumn?.RowCount ?? (perRow.Count > 0 ? perRow[0].RowCount : context.Chunk.RowCount);
        var data = new object?[rowCount];
        for (var i = 0; i < rowCount; i++)
            data[i] = Test(node, leftColumn is null ? leftScalar : leftColumn.GetRawDataValue(i), fixedKeys, perRow, i, comparer);
        return new ColumnarResult(ColumnHelpers.CreateFromObjectArray(data, ScalarTypes.Bool));
    }

    private static bool Test(IRInListNode node, object? leftValue, HashSet<string> fixedKeys, List<BaseColumn> perRow, int row,
        StringComparer comparer)
    {
        // KQL strings have no null: a null string compares as the empty string (as the other string operators do).
        if (leftValue is null && node.Domain == InListDomain.String && node.Left.ResultType.Simplify() == ScalarTypes.String)
            leftValue = string.Empty;
        var key = InListKeys.Key(leftValue, node.Domain);
        if (key is null) return node.Negated; // a null never equals a list value
        var found = fixedKeys.Contains(key);
        for (var c = 0; !found && c < perRow.Count; c++)
        {
            var rowKeys = new HashSet<string>(comparer);
            AddKeys(rowKeys, perRow[c].GetRawDataValue(row), node.Domain);
            found = rowKeys.Contains(key);
        }
        return found != node.Negated;
    }

    /// <summary>A dynamic array contributes its elements; anything else contributes itself.</summary>
    private static void AddKeys(HashSet<string> keys, object? value, InListDomain domain)
    {
        if (value is JsonArray array)
        {
            foreach (var item in array)
                if (InListKeys.Key(item, domain) is { } k) keys.Add(k);
            return;
        }
        if (InListKeys.Key(value, domain) is { } key) keys.Add(key);
    }
}

/// <summary>The comparison key of a value in an <see cref="InListDomain"/> (null when the value has none: it never matches).
/// A dynamic value converts as ADX's <c>tostring</c>/<c>tolong</c>/<c>todouble</c>/<c>tobool</c>/<c>todatetime</c> would.</summary>
internal static class InListKeys
{
    public static string? Key(object? value, InListDomain domain)
    {
        if (value is null or DBNull) return null;
        if (value is JsonValue jv) value = Unwrap(jv);
        return domain switch
        {
            InListDomain.String => AsString(value),
            InListDomain.Long => AsLong(value) is { } l ? l.ToString(CultureInfo.InvariantCulture) : null,
            InListDomain.Real => AsReal(value) is { } d ? d.ToString("R", CultureInfo.InvariantCulture) : null,
            InListDomain.Bool => value switch
            {
                bool b => b ? "true" : "false",
                string s when bool.TryParse(s, out var b) => b ? "true" : "false",
                _ => null,
            },
            InListDomain.DateTime => value switch
            {
                DateTime t => t.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
                string s when DateTime.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
                    => t.Ticks.ToString(CultureInfo.InvariantCulture),
                _ => null,
            },
            InListDomain.TimeSpan => value switch
            {
                TimeSpan t => t.Ticks.ToString(CultureInfo.InvariantCulture),
                string s when TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var t) => t.Ticks.ToString(CultureInfo.InvariantCulture),
                _ => null,
            },
            InListDomain.Guid => value switch
            {
                Guid g => g.ToString("D"),
                string s when Guid.TryParse(s, out var g) => g.ToString("D"),
                _ => null,
            },
            _ => null,
        };
    }

    private static object Unwrap(JsonValue jv) =>
        jv.GetValueKind() switch
        {
            JsonValueKind.String => jv.GetValue<string>(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => jv.TryGetValue<long>(out var l) ? l : jv.TryGetValue<double>(out var d) ? d : jv.ToJsonString(),
            _ => jv.TryGetValue<object>(out var o) && o is not JsonElement ? o : jv.ToJsonString(),
        };

    private static string AsString(object value) => value switch
    {
        string s => s,
        bool b => b ? "true" : "false",
        JsonNode n => n.ToJsonString(),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static long? AsLong(object value) => value switch
    {
        long l => l,
        int i => i,
        double d when d == Math.Floor(d) && d >= long.MinValue && d <= long.MaxValue => (long)d,
        decimal m when m == decimal.Floor(m) => (long)m,
        string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) => l,
        _ => null,
    };

    private static double? AsReal(object value) => value switch
    {
        double d => d,
        long l => l,
        int i => i,
        decimal m => (double)m,
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
        _ => null,
    };
}
