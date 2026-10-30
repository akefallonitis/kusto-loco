//
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KustoLoco.Core.Util;

/// <summary>
///     Types the strings inside a dynamic value the way ADX does when it builds one from JSON text.
/// </summary>
/// <remarks>
///     https://learn.microsoft.com/kusto/query/scalar-data-types/dynamic
///     "Kusto will parse strings as strongly-typed values if they can be parsed as such."
///     Checked against the ADX engine (kustainer), this happens wherever ADX reads JSON text into a
///     dynamic value - parse_json / todynamic of a string, dynamic literals (also inside datatable)
///     and ingestion - and it applies to exactly two kinds of string:
///     <list type="bullet">
///         <item>
///             an ISO 8601 date or date-time becomes a datetime, normalised to UTC and written back as
///             yyyy-MM-ddTHH:mm:ss.fffffffZ ("2015-01-01" and "2015-01-01T10:20:30+02:00" read as
///             "2015-01-01T00:00:00.0000000Z" and "2015-01-01T08:20:30.0000000Z");
///         </item>
///         <item>
///             a GUID in its 8-4-4-4-12 form becomes a guid, written back in lower case.
///         </item>
///     </list>
///     A string that holds a number ("123", "1.5"), a bool or a timespan stays a string, and so does
///     every string built by a function (bag_pack, pack_array, split...) or held in a property name.
///     <para />
///     KustoLoco represents a dynamic scalar by its JSON value and converts it through its text
///     (todatetime, toguid, comparison with a string...), so a typed value is kept as the text ADX
///     gives it: that is what tostring, comparisons, display and serialization observe.
///     <para />
///     <see cref="Apply" /> is public so that a host that builds dynamic columns itself can type them
///     as ADX ingestion does.
/// </remarks>
public static class DynamicStringTyping
{
    private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    /// <summary>
    ///     Types every string in <paramref name="node" /> that ADX would read as a datetime or a guid.
    ///     Arrays and property bags are updated in place; a scalar string is returned as a new value.
    /// </summary>
    public static JsonNode? Apply(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject bag:
                List<KeyValuePair<string, JsonNode?>>? replaced = null;
                foreach (var (key, value) in bag)
                {
                    var typed = Apply(value);
                    if (!ReferenceEquals(typed, value))
                        (replaced ??= []).Add(new KeyValuePair<string, JsonNode?>(key, typed));
                }

                if (replaced != null)
                    foreach (var (key, value) in replaced)
                        bag[key] = value;
                return bag;

            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var value = array[i];
                    var typed = Apply(value);
                    if (!ReferenceEquals(typed, value))
                        array[i] = typed;
                }

                return array;

            case JsonValue value when value.GetValueKind() == JsonValueKind.String &&
                                      value.TryGetValue<string>(out var text) &&
                                      TryGetTypedText(text, out var typedText) &&
                                      typedText != text:
                // backed by a JsonElement, like the parsed value it replaces, so that it still converts with
                // TryGetValue<DateTime> / TryGetValue<Guid>
                return JsonValue.Create(JsonSerializer.SerializeToElement(typedText));

            default:
                return node;
        }
    }

    /// <summary>
    ///     The text ADX gives <paramref name="text" /> once typed, when it reads as a datetime or a guid
    /// </summary>
    public static bool TryGetTypedText(string text, out string typedText)
    {
        if (TryParseGuid(text, out var guid))
        {
            typedText = guid.ToString("D");
            return true;
        }

        if (TryParseDateTime(text, out var dateTime))
        {
            typedText = dateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
            return true;
        }

        typedText = text;
        return false;
    }

    /// <summary>
    ///     A guid in the 8-4-4-4-12 form, in either case, with nothing around it
    /// </summary>
    /// <remarks>
    ///     ADX leaves "{...}", "(...)", the 32-digit form and a guid with surrounding spaces as strings.
    /// </remarks>
    internal static bool TryParseGuid(ReadOnlySpan<char> s, out Guid guid)
    {
        guid = Guid.Empty;
        if (s.Length != 36)
            return false;

        for (var i = 0; i < s.Length; i++)
        {
            var isSeparator = i is 8 or 13 or 18 or 23;
            if (isSeparator ? s[i] != '-' : !char.IsAsciiHexDigit(s[i]))
                return false;
        }

        return Guid.TryParseExact(s, "D", out guid);
    }

    /// <summary>
    ///     An ISO 8601 date, or date and time, as ADX reads one inside a dynamic value
    /// </summary>
    /// <remarks>
    ///     The accepted forms, each checked against the ADX engine (kustainer):
    ///     <code>
    ///     yyyy-MM-dd [ ('T' | whitespace) H:m [:s [.fraction]] [whitespace] [zone] ] [whitespace]
    ///     zone   = ('Z' | 'UTC' | 'GMT') [offset] | offset          (letters in any case)
    ///     offset = ('+' | '-') (h | hh | hhm | hhmm | h[h]:m[m])
    ///     </code>
    ///     The date takes exactly four, two and two digits; the hour, minute and second one or two;
    ///     the fraction any number of digits, of which the first seven count. A time without a zone is
    ///     UTC. Whitespace is space, tab, CR or LF, and none is allowed in front. A date alone cannot
    ///     carry a zone ("2015-01-01Z" stays a string), nor can an hour alone ("2015-01-01T10").
    ///     Like ADX, the offset is not range-checked ("+24:00" and "+05:60" apply as written), and a
    ///     value that falls outside 0001-01-01 .. 9999-12-31 once the offset is applied stays a string.
    /// </remarks>
    internal static bool TryParseDateTime(ReadOnlySpan<char> s, out DateTime utc)
    {
        utc = default;
        var p = 0;
        if (!Number(s, ref p, 4, 4, out var year) || !Accept(s, ref p, '-') ||
            !Number(s, ref p, 2, 2, out var month) || !Accept(s, ref p, '-') ||
            !Number(s, ref p, 2, 2, out var day))
            return false;

        if (year < 1 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            return false;

        var ticks = new DateTime(year, month, day).Ticks;
        var afterDate = p;
        SkipWhitespace(s, ref p);
        if (p == s.Length)
        {
            utc = new DateTime(ticks, DateTimeKind.Utc);
            return true;
        }

        // the time follows either a 'T' straight after the date or whitespace
        if (p == afterDate && !Accept(s, ref p, 'T'))
            return false;

        if (!Number(s, ref p, 1, 2, out var hour) || !Accept(s, ref p, ':') ||
            !Number(s, ref p, 1, 2, out var minute))
            return false;

        var second = 0;
        long fraction = 0;
        if (Accept(s, ref p, ':'))
        {
            if (!Number(s, ref p, 1, 2, out second))
                return false;

            if (Accept(s, ref p, '.'))
            {
                var digits = 0;
                for (; p < s.Length && char.IsAsciiDigit(s[p]); p++)
                {
                    if (digits == 7)
                        continue;
                    fraction = fraction * 10 + (s[p] - '0');
                    digits++;
                }

                for (; digits < 7; digits++)
                    fraction *= 10;
            }
        }

        if (hour > 23 || minute > 59 || second > 59)
            return false;

        ticks += hour * TimeSpan.TicksPerHour + minute * TimeSpan.TicksPerMinute +
                 second * TimeSpan.TicksPerSecond + fraction;

        SkipWhitespace(s, ref p);
        if (p < s.Length && s[p] is 'Z' or 'z')
            p++;
        else if (Word(s, p, "UTC") || Word(s, p, "GMT"))
            p += 3;

        if (p < s.Length && s[p] is '+' or '-')
        {
            var sign = s[p] == '+' ? 1 : -1;
            p++;
            if (!Offset(s, ref p, out var offsetMinutes))
                return false;
            ticks -= sign * offsetMinutes * TimeSpan.TicksPerMinute;
        }

        SkipWhitespace(s, ref p);
        if (p != s.Length || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            return false;

        utc = new DateTime(ticks, DateTimeKind.Utc);
        return true;
    }

    // h, hh, hhm, hhmm or h[h]:m[m], as minutes: without a colon the first two digits are the
    // hours ("+123" is 12 hours 3 minutes, as in ADX)
    private static bool Offset(ReadOnlySpan<char> s, ref int p, out int minutes)
    {
        minutes = 0;
        var start = p;
        while (p < s.Length && char.IsAsciiDigit(s[p]))
            p++;
        var digits = p - start;
        if (digits is < 1 or > 4)
            return false;

        var hourDigits = Math.Min(digits, 2);
        var hours = Value(s.Slice(start, hourDigits));
        var mins = Value(s.Slice(start + hourDigits, digits - hourDigits));
        if (Accept(s, ref p, ':'))
        {
            if (digits > 2 || !Number(s, ref p, 1, 2, out mins))
                return false;
        }

        minutes = hours * 60 + mins;
        return true;
    }

    private static bool Number(ReadOnlySpan<char> s, ref int p, int minDigits, int maxDigits, out int value)
    {
        var start = p;
        while (p < s.Length && p - start < maxDigits && char.IsAsciiDigit(s[p]))
            p++;
        value = Value(s.Slice(start, p - start));
        return p - start >= minDigits;
    }

    private static int Value(ReadOnlySpan<char> digits)
    {
        var value = 0;
        foreach (var c in digits)
            value = value * 10 + (c - '0');
        return value;
    }

    private static bool Accept(ReadOnlySpan<char> s, ref int p, char c)
    {
        if (p >= s.Length || s[p] != c)
            return false;
        p++;
        return true;
    }

    private static bool Word(ReadOnlySpan<char> s, int p, string word) =>
        s.Length - p >= word.Length && s.Slice(p, word.Length).Equals(word, StringComparison.OrdinalIgnoreCase);

    private static void SkipWhitespace(ReadOnlySpan<char> s, ref int p)
    {
        while (p < s.Length && s[p] is ' ' or '\t' or '\r' or '\n')
            p++;
    }
}
