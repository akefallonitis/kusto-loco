//
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoLoco.Core;

/// <summary>
/// An <see cref="IKustoQueryContextTableLoader"/> that serves a folder of delimited files as tables: a query
/// referencing <c>Watchlist</c> is answered by <c>Watchlist.csv</c> in the folder. Files are read on demand — only
/// the tables a query actually names are touched — and a table already present in the context is left alone.
/// </summary>
/// <remarks>
/// <para><b>Files.</b> <c>&lt;TableName&gt;</c> plus any supported delimited extension, optionally gzipped:
/// <c>.csv</c>, <c>.tsv</c>, <c>.psv</c>, <c>.scsv</c>, <c>.sohsv</c>, <c>.tsve</c>, each also as <c>.gz</c>.
/// The delimiter follows the extension, so a <c>.tsv</c> is split on tabs. Matching is case-insensitive.</para>
/// <para><b>Columns.</b> The first row is a header. A bare name (<c>Cidr</c>) declares a string column; a name
/// may carry a type (<c>Port:int</c>, <c>Seen:datetime</c>) using the KQL scalar type names — string, bool, int,
/// long, real, decimal, datetime, timespan, guid, dynamic. Typing columns lets a rule use native predicates
/// rather than string comparison.</para>
/// <para><b>Bounds and failure.</b> <see cref="MaxRows"/> caps how many data rows a single file contributes. A
/// referenced table with no matching file is simply not added — the query then fails with the engine's usual
/// unknown-table error rather than silently matching nothing; an unreadable or malformed file throws.</para>
/// </remarks>
public sealed class DelimitedFolderTableLoader : IKustoQueryContextTableLoader
{
    /// <summary>Extensions probed for a table, in order; each is also probed with a trailing <c>.gz</c>.</summary>
    private static readonly string[] Extensions = [".csv", ".tsv", ".tsve", ".psv", ".scsv", ".sohsv"];

    private readonly string _folder;

    /// <summary>Serve <paramref name="folder"/> as a set of tables.</summary>
    /// <param name="maxRows">Maximum data rows taken from one file (default 1,000,000).</param>
    public DelimitedFolderTableLoader(string folder, int maxRows = 1_000_000)
    {
        if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("folder must be provided.", nameof(folder));
        if (maxRows <= 0) throw new ArgumentOutOfRangeException(nameof(maxRows), maxRows, "maxRows must be positive.");
        _folder = folder;
        MaxRows = maxRows;
    }

    /// <summary>Maximum data rows taken from a single file.</summary>
    public int MaxRows { get; }

    /// <inheritdoc />
    public Task LoadTablesAsync(KustoQueryContext context, IReadOnlyCollection<string> tableNames)
    {
        var present = context.TableNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in tableNames)
        {
            if (present.Contains(name)) continue;              // already supplied by the host — never override it
            if (FindFile(name) is not { } file) continue;      // unknown table: let the engine report it
            context.AddTable(Load(name, file));
        }
        return Task.CompletedTask;
    }

    /// <summary>The file backing <paramref name="tableName"/>, or null when the folder has none.</summary>
    public string? FindFile(string tableName)
    {
        if (!Directory.Exists(_folder)) return null;
        foreach (var extension in Extensions)
        foreach (var candidate in new[] { tableName + extension, tableName + extension + ".gz" })
        {
            var path = Path.Combine(_folder, candidate);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private TableBuilder Load(string tableName, string path)
    {
        var delimiter = DelimitedTextParser.DelimiterFor(ExtensionFormat(path));
        var rows = DelimitedTextParser.Parse(ReadAllText(path), delimiter);
        if (rows.Count == 0)
            throw new InvalidOperationException($"table file '{path}' is empty (a header row is required).");

        var header = rows[0];
        var columns = header.Select(ParseHeader).ToArray();
        var data = rows.Skip(1).Take(MaxRows).ToArray();

        var builder = TableBuilder.CreateEmpty(tableName, data.Length);
        for (var c = 0; c < columns.Length; c++)
        {
            var (name, type) = columns[c];
            var cells = new object?[data.Length];
            for (var r = 0; r < data.Length; r++)
                cells[r] = c < data[r].Count ? Convert(data[r][c], type) : null;
            builder = builder.WithColumn(name, type, cells);
        }
        return builder;
    }

    // "Name" => string; "Name:type" => that KQL scalar type. An unrecognised type name is an error rather than a
    // silent downgrade to string, so a typo in a header cannot quietly disable typed predicates.
    private static (string Name, Type Type) ParseHeader(string cell)
    {
        var text = cell.Trim();
        var separator = text.LastIndexOf(':');
        if (separator <= 0) return (text, typeof(string));

        var name = text[..separator].Trim();
        var declared = text[(separator + 1)..].Trim().ToLowerInvariant();
        var type = declared switch
        {
            "string" => typeof(string),
            "bool" or "boolean" => typeof(bool),
            "int" => typeof(int),
            "long" => typeof(long),
            "real" or "double" => typeof(double),
            "decimal" => typeof(decimal),
            "datetime" or "date" => typeof(DateTime),
            "timespan" or "time" => typeof(TimeSpan),
            "guid" or "uuid" => typeof(Guid),
            "dynamic" => typeof(object),
            _ => throw new InvalidOperationException(
                $"column '{name}' declares unknown type '{declared}'; use string|bool|int|long|real|decimal|datetime|timespan|guid|dynamic."),
        };
        return (name, type);
    }

    // Parse with the invariant culture so a file means the same thing on every machine, and treat an unparseable
    // cell as null rather than failing the load — one bad value must not cost the whole table.
    private static object? Convert(string? text, Type type)
    {
        if (text is null) return null;
        if (type == typeof(string)) return text;
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return null;

        if (type == typeof(bool))
            return bool.TryParse(trimmed, out var b) ? b
                : long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bi) ? bi != 0
                : null;
        if (type == typeof(int)) return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
        if (type == typeof(long)) return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null;
        if (type == typeof(double)) return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        if (type == typeof(decimal)) return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var m) ? m : null;
        if (type == typeof(DateTime))
            return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt) ? dt : null;
        if (type == typeof(TimeSpan)) return TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out var ts) ? ts : null;
        if (type == typeof(Guid)) return Guid.TryParse(trimmed, out var g) ? g : null;
        return text;
    }

    private static string ExtensionFormat(string path)
    {
        var name = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? path[..^3] : path;
        return Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
    }

    private static string ReadAllText(string path)
    {
        if (!path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)) return File.ReadAllText(path);
        using var file = File.OpenRead(path);
        using var gz = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gz, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
