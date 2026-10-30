using System;
using System.Text.Json.Nodes;
using KustoLoco.Core.Util;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

[KustoImplementation(Keyword = "Functions.ArrayIndexOf")]
internal partial class ArrayIndexOfFunction
{
    internal static long StringImpl(JsonNode itemArray, string value)
        => ArrayIndexOfCore.StringImpl(itemArray, value, 0, -1, 1);

    internal static long StringStartImpl(JsonNode itemArray, string value, long start)
        => ArrayIndexOfCore.StringImpl(itemArray, value, start, -1, 1);

    internal static long StringStartLengthImpl(JsonNode itemArray, string value, long start, long length)
        => ArrayIndexOfCore.StringImpl(itemArray, value, start, length, 1);

    internal static long StringStartLengthOccurenceImpl(JsonNode itemArray, string value, long start, long length,
        long occurence)
        => ArrayIndexOfCore.StringImpl(itemArray, value, start, length, occurence);


    internal static long LongImpl(JsonNode itemArray, long value)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, 0, -1, 1);

    internal static long LongStartImpl(JsonNode itemArray, long value, long start)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, -1, 1);

    internal static long LongStartLengthImpl(JsonNode itemArray, long value, long start, long length)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, length, 1);

    internal static long LongStartLengthOccurenceImpl(JsonNode itemArray, long value, long start, long length,
        long occurence)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, length, occurence);

    internal static long DateTimeImpl(JsonNode itemArray, DateTime value)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, 0, -1, 1);

    internal static long DateTimeStartImpl(JsonNode itemArray, DateTime value, long start)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, -1, 1);

    internal static long DateTimeStartLengthImpl(JsonNode itemArray, DateTime value, long start, long length)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, length, 1);

    internal static long DateTimeStartLengthOccurenceImpl(JsonNode itemArray, DateTime value, long start, long length,
        long occurence)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, length, occurence);

    internal static long IntImpl(JsonNode itemArray, int value)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, 0, -1, 1);

    internal static long IntStartImpl(JsonNode itemArray, int value, long start)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, -1, 1);

    internal static long IntStartLengthImpl(JsonNode itemArray, int value, long start, long length)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, length, 1);

    internal static long IntStartLengthOccurenceImpl(JsonNode itemArray, int value, long start, long length,
        long occurence)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, length, occurence);


    internal static long TimeSpanImpl(JsonNode itemArray, TimeSpan value)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, 0, -1, 1);

    internal static long TimeSpanStartImpl(JsonNode itemArray, TimeSpan value, long start)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, -1, 1);

    internal static long TimeSpanStartLengthImpl(JsonNode itemArray, TimeSpan value, long start, long length)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, length, 1);

    internal static long TimeSpanStartLengthOccurenceImpl(JsonNode itemArray, TimeSpan value, long start, long length,
        long occurence)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, length, occurence);


    internal static long GuidImpl(JsonNode itemArray, Guid value)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, 0, -1, 1);

    internal static long GuidStartImpl(JsonNode itemArray, Guid value, long start)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, -1, 1);

    internal static long GuidStartLengthImpl(JsonNode itemArray, Guid value, long start, long length)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, length, 1);

    internal static long GuidStartLengthOccurenceImpl(JsonNode itemArray, Guid value, long start, long length,
        long occurence)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, length, occurence);

    internal static long BoolImpl(JsonNode itemArray, bool value)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, 0, -1, 1);

    internal static long BoolStartImpl(JsonNode itemArray, bool value, long start)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, -1, 1);

    internal static long BoolStartLengthImpl(JsonNode itemArray, bool value, long start, long length)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, length, 1);

    internal static long BoolStartLengthOccurenceImpl(JsonNode itemArray, bool value, long start, long length,
        long occurence)
        => ArrayIndexOfCore.StartLengthOccurenceImpl(itemArray, value, start, length, occurence);
}

internal static class ArrayIndexOfCore
{
    public static long StartLengthOccurenceImpl<T>(JsonNode itemArray, T value, long start, long length, long occurence)
        where T : IEquatable<T>
        => Find(itemArray, start, length, occurence, v => v.TryGetValue<T>(out var s) && s.Equals(value));

    // ADX converts the value to the type of the element it is compared with, so a string that reads as a datetime or a
    // guid also finds the element that parse_json or a dynamic literal typed from the same text
    // (array_index_of(parse_json('["2015-01-01"]'), "2015-01-01") is 0): see DynamicStringTyping.
    public static long StringImpl(JsonNode itemArray, string value, long start, long length, long occurence)
    {
        var typed = DynamicStringTyping.TryGetTypedText(value, out var t) ? t : value;
        return Find(itemArray, start, length, occurence,
            v => v.TryGetValue<string>(out var s) && (s == value || s == typed));
    }

    private static long Find(JsonNode itemArray, long start, long length, long occurence, Func<JsonValue, bool> matches)
    {
        if (itemArray is not JsonArray items)
            return -1;
        var pos = start;
        if (pos < 0) pos = items.Count + start;
        pos = Math.Clamp(pos, 0, items.Count);

        var last =
            length < 0 ? items.Count : Math.Min(items.Count, pos + length);
        for (var i = pos; i < last; i++)
        {
            if (items[(int)i] is not JsonValue v || !matches(v)) continue;
            occurence--;
            if (occurence <= 0)
                return i;
        }

        return -1;
    }
}
