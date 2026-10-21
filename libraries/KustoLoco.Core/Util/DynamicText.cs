using System;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace KustoLoco.Core.Util;

/// <summary>
///     Renders dynamic values as text the way ADX does.
/// </summary>
/// <remarks>
///     https://learn.microsoft.com/kusto/query/tostring-function
///     ADX writes compact JSON and escapes only what JSON requires inside a string: the quote,
///     the backslash and control characters below U+0020 (\b \f \n \r \t, otherwise \u00XX).
///     Every other character - including &lt; &gt; &amp; ', non-ASCII letters, U+007F,
///     U+2028 and characters outside the BMP - is written as it is. That was checked against
///     the ADX engine (kustainer).
///     System.Text.Json's default encoder escapes HTML-sensitive and non-ASCII characters, and
///     even UnsafeRelaxedJsonEscaping still escapes some (U+007F-U+00A0, U+2028, surrogate
///     pairs...), so the relaxed output is post-processed to undo every \uXXXX escape that ADX
///     does not write.
/// </remarks>
public static class DynamicText
{
    private static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // values built from CLR objects (JsonValue.Create(...)) need a resolver to serialize
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions Indented = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        WriteIndented = true
    };

    /// <summary>
    ///     The value as compact JSON text, as tostring(dynamic) gives it for an array or a bag
    /// </summary>
    public static string ToJson(JsonNode node) => Unescape(node.ToJsonString(Compact));

    /// <summary>
    ///     The value as indented JSON text with ADX escaping, for display
    /// </summary>
    public static string ToIndentedJson(JsonNode node) => Unescape(node.ToJsonString(Indented));

    /// <summary>
    ///     The value converted to a string as tostring() does: a string value is returned as it is
    ///     (without quotes), null becomes the empty string and anything else is compact JSON
    /// </summary>
    public static string ToKustoString(JsonNode? node) =>
        node switch
        {
            null => string.Empty,
            JsonValue value when value.GetValueKind() == JsonValueKind.Null => string.Empty,
            JsonValue value when value.GetValueKind() == JsonValueKind.String => StringOf(value),
            _ => ToJson(node)
        };

    /// <summary>
    ///     As <see cref="ToKustoString" /> but with indented JSON, for display
    /// </summary>
    public static string ToDisplayString(JsonNode node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? StringOf(value)
            : ToIndentedJson(node);

    // a JsonValue of kind String may hold a CLR string, a JsonElement or another CLR type that
    // serializes as a JSON string (a Guid, a DateTime...)
    private static string StringOf(JsonValue value) =>
        value.TryGetValue<string>(out var s)
            ? s
            : JsonSerializer.Deserialize<string>(value.ToJsonString()) ?? string.Empty;

    /// <summary>
    ///     Replaces every \uXXXX escape for a character at or above U+0020 with the character itself
    /// </summary>
    /// <remarks>
    ///     The text is valid JSON, so a backslash only occurs inside a string and always starts an
    ///     escape sequence; walking the escapes in order keeps "\\u0041" (an escaped backslash
    ///     followed by "u0041") intact.
    /// </remarks>
    private static string Unescape(string json)
    {
        if (!json.Contains("\\u"))
            return json;

        var builder = new StringBuilder(json.Length);
        var i = 0;
        while (i < json.Length)
        {
            var c = json[i];
            if (c != '\\' || i + 1 >= json.Length)
            {
                builder.Append(c);
                i++;
                continue;
            }

            var next = json[i + 1];
            if (next == 'u' && i + 5 < json.Length &&
                int.TryParse(json.AsSpan(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                    out var code) &&
                code >= 0x20)
            {
                // the relaxed encoder writes a quote or a backslash as a short escape, never in
                // this form, but if one turns up it must stay escaped
                if (code == '"' || code == '\\')
                    builder.Append('\\').Append((char)code);
                else
                    builder.Append((char)code);
                i += 6;
                continue;
            }

            // any other escape (\" \\ \n \u0001 ...) is copied unchanged
            builder.Append(c).Append(next);
            i += 2;
        }

        return builder.ToString();
    }
}
