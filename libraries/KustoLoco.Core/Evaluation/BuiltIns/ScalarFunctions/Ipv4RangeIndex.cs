//
// Licensed under the MIT License.

// The range index behind ipv4_lookup: https://learn.microsoft.com/kusto/query/ipv4-lookup-plugin

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using KustoLoco.Core.DataSource;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

/// <summary>
/// For each prefix length present in the lookup column, the ranges' network addresses SORTED, each with the lookup rows
/// that carry it; an address is looked up by one binary search per prefix length (at most 33), and the hits are returned
/// in ascending row order, so the operator emits rows in lookup-table order as the nested loop did.
/// <para>
/// Parsing follows the ADX engine (checked against kustainer) rather than Ipv4Support.InRange: a value
/// is trimmed of surrounding white space; the address must be four dot-separated decimal octets, each 0–255 with no leading
/// zero ("010.1.2.3", "10.1.2", hex and five-octet forms never match); an optional "/prefix" must be an integer 0–32 written
/// with digits and an optional sign ("/024" and "/+24" are 24; "/ 24", "/", "/0x8", "/8.0", "/33" never match); a range
/// with no prefix is a /32. A source address may carry a prefix too: it matches the ranges whose prefix is not longer than
/// its own and that contain its network (10.1.0.0/16 is in 10.0.0.0/8, not in 10.1.2.0/24).
/// </para>
/// </summary>
internal sealed class Ipv4RangeIndex
{
    private static readonly ConditionalWeakTable<ITableSource, Dictionary<int, Ipv4RangeIndex>> Cache = new();

    private readonly (int Prefix, uint[] Networks, int[][] Rows)[] _byPrefix;

    private Ipv4RangeIndex((int, uint[], int[][])[] byPrefix) => _byPrefix = byPrefix;

    /// <summary>The index of <paramref name="column"/> over <paramref name="chunk"/> (the reassembled
    /// <paramref name="table"/>), built once per table instance and column.</summary>
    public static Ipv4RangeIndex For(ITableSource table, ITableChunk chunk, int column)
    {
        var perTable = Cache.GetOrCreateValue(table);
        lock (perTable)
        {
            if (!perTable.TryGetValue(column, out var index))
                perTable[column] = index = Build(chunk, column);
            return index;
        }
    }

    public static Ipv4RangeIndex Build(ITableChunk chunk, int column)
    {
        var groups = new SortedDictionary<int, SortedDictionary<uint, List<int>>>();
        var values = chunk.Columns[column];
        for (var r = 0; r < chunk.RowCount; r++)
        {
            if (Parse(values.GetRawDataValue(r)?.ToString()) is not var (prefix, network)) continue;
            if (!groups.TryGetValue(prefix, out var byNetwork)) groups[prefix] = byNetwork = new SortedDictionary<uint, List<int>>();
            if (!byNetwork.TryGetValue(network, out var rows)) byNetwork[network] = rows = new List<int>();
            rows.Add(r);
        }
        var result = new List<(int, uint[], int[][])>();
        foreach (var (prefix, byNetwork) in groups)
        {
            var networks = new uint[byNetwork.Count];
            var rows = new int[byNetwork.Count][];
            var i = 0;
            foreach (var (network, list) in byNetwork)
            {
                networks[i] = network;
                rows[i++] = list.ToArray();
            }
            result.Add((prefix, networks, rows));
        }
        return new Ipv4RangeIndex(result.ToArray());
    }

    /// <summary>The lookup rows whose range contains <paramref name="ip"/>, ascending; empty when it is not IPv4.</summary>
    public List<int> Rows(string? ip)
    {
        var found = new List<int>();
        if (Parse(ip) is not var (sourcePrefix, sourceNetwork)) return found;
        foreach (var (prefix, networks, rows) in _byPrefix)
        {
            if (prefix > sourcePrefix) break; // groups are in ascending prefix order
            var at = Array.BinarySearch(networks, sourceNetwork & Mask(prefix));
            if (at >= 0) found.AddRange(rows[at]);
        }
        found.Sort();
        return found;
    }

    private static uint Mask(int prefix) => prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);

    /// <summary>An IPv4 address or range as ADX reads it (see the class remarks): its prefix (32 when absent) and its network
    /// address under that prefix; null when ADX would not read it.</summary>
    public static (int Prefix, uint Network)? Parse(string? value)
    {
        if (value is null) return null;
        var s = value.Trim();
        if (s.Length == 0) return null;
        var slash = s.IndexOf('/');
        var address = slash < 0 ? s : s.Substring(0, slash);
        var prefix = 32;
        if (slash >= 0)
        {
            var p = s.Substring(slash + 1);
            if (!int.TryParse(p, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out prefix) || prefix < 0 || prefix > 32)
                return null;
        }
        var parts = address.Split('.');
        if (parts.Length != 4) return null;
        uint v = 0;
        foreach (var part in parts)
        {
            if (part.Length is 0 or > 3 || (part.Length > 1 && part[0] == '0')) return null;
            var octet = 0;
            foreach (var c in part)
            {
                if (c is < '0' or > '9') return null;
                octet = octet * 10 + (c - '0');
            }
            if (octet > 255) return null;
            v = (v << 8) | (uint)octet;
        }
        return (prefix, v & Mask(prefix));
    }
}
