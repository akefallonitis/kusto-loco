using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     ipv4_lookup through a range index: the same rows, in lookup-table order, as the nested loop it replaces,
///     with ranges and addresses read as the ADX engine reads them.
///     https://learn.microsoft.com/kusto/query/ipv4-lookup-plugin
///     The expected rows were checked against the ADX engine (kustainer); ADX does not promise the lookup's row
///     order, so that was compared as a multiset.
/// </summary>
[TestClass]
public class Ipv4LookupRangeIndexTests : TestMethods
{
    // Well-formed ranges: nested, duplicated, a /0, a /32 and an unaligned base.
    private const string WellFormed = """
                                      let R = datatable(network:string, label:string, tenant:string) [
                                      "10.0.0.0/8", "corp", "a",
                                      "10.1.0.0/16", "site", "a",
                                      "10.1.2.0/24", "lab", "b",
                                      "192.168.1.10/32", "host", "a",
                                      "10.1.2.0/24", "lab-dup", "a",
                                      "0.0.0.0/0", "any", "b",
                                      "172.16.5.77/12", "unaligned-base", "b"
                                      ];
                                      let S = datatable(Id:long, ip:string, tenant:string) [
                                      1, "10.1.2.3", "a",
                                      2, "10.9.9.9", "b",
                                      3, "192.168.1.10", "a",
                                      4, "8.8.8.8", "b",
                                      5, "not an ip", "a",
                                      6, "", "a",
                                      8, "172.31.0.1", "b",
                                      9, "10.1.2.3", "b",
                                      10, "2001:db8::1", "a"
                                      ];
                                      """;

    [TestMethod]
    [DataRow("S | evaluate ipv4_lookup(R, ip, network) | project Id, label",
        "1,corp;1,site;1,lab;1,lab-dup;1,any;2,corp;2,any;3,host;3,any;4,any;8,any;8,unaligned-base;9,corp;9,site;9,lab;9,lab-dup;9,any")]
    [DataRow("S | evaluate ipv4_lookup(R, ip, network, return_unmatched = true) | project Id, label",
        "1,corp;1,site;1,lab;1,lab-dup;1,any;2,corp;2,any;3,host;3,any;4,any;5,;6,;8,any;8,unaligned-base;9,corp;9,site;9,lab;9,lab-dup;9,any;10,")]
    [DataRow("S | evaluate ipv4_lookup(R, ip, network, tenant) | project Id, label",
        "1,corp;1,site;1,lab-dup;2,any;3,host;4,any;8,any;8,unaligned-base;9,lab;9,any")]
    [DataRow("S | evaluate ipv4_lookup(R, ip, network, tenant, return_unmatched = true) | project Id, label",
        "1,corp;1,site;1,lab-dup;2,any;3,host;4,any;5,;6,;8,any;8,unaligned-base;9,lab;9,any;10,")]
    public async Task WellFormedRanges(string query, string expected)
    {
        var result = await ResultAsString(WellFormed + query, ";");
        result.Should().Be(expected);
    }
    // Malformed and unusual ranges and addresses, each read as the ADX engine reads it: surrounding white space is
    // trimmed; an address is four decimal octets 0-255 without leading zeros; a prefix is an integer 0-32 with an
    // optional sign ("/024" and "/+24" are /24; "/ 24", "/33", "/-1" and "/" never match); a range without a prefix
    // is a /32; and a source address may carry a prefix, matching the ranges no longer than it that contain it.
    private const string Malformed = """
                                     let R = datatable(network:string, label:string) [
                                     "10.1.2.3", "noslash",
                                     "10.1.2.0/ 24", "padded",
                                     "10.1.2.0/024", "zeropad",
                                     "10.1.2.0/+24", "plus",
                                     "010.001.002.000/24", "leadzero",
                                     "10.1.2/24", "short",
                                     "0x0A.1.2.0/24", "hex",
                                     " 10.1.2.0/24", "lspace",
                                     "10.1.2.0/24 ", "tspace",
                                     "10.1.2.0/33", "p33",
                                     "10.1.2.0/-1", "neg",
                                     "10.1.2.0/", "emptyp",
                                     "10.0.0.0/8", "n8",
                                     "10.1.2.0/24", "n24"
                                     ];
                                     let S = datatable(ip:string) [
                                     "10.1.2.3", "010.1.2.3", "10.1.2", "10.1.2.3/24", " 10.1.2.3", "10.1.0.0/16", "10.1.2.3/40"
                                     ];
                                     S | evaluate ipv4_lookup(R, ip, network) | project ip, label
                                     """;

    [TestMethod]
    public async Task MalformedRanges_ReadAsAdx()
    {
        var expected = new[]
        {
            " 10.1.2.3,lspace", " 10.1.2.3,n24", " 10.1.2.3,n8", " 10.1.2.3,noslash", " 10.1.2.3,plus",
            " 10.1.2.3,tspace", " 10.1.2.3,zeropad",
            "10.1.0.0/16,n8",
            "10.1.2.3,lspace", "10.1.2.3,n24", "10.1.2.3,n8", "10.1.2.3,noslash", "10.1.2.3,plus",
            "10.1.2.3,tspace", "10.1.2.3,zeropad",
            "10.1.2.3/24,lspace", "10.1.2.3/24,n24", "10.1.2.3/24,n8", "10.1.2.3/24,plus",
            "10.1.2.3/24,tspace", "10.1.2.3/24,zeropad"
        };
        var result = await Result(Malformed);
        var rows = result.EnumerateRows().Select(RowString).Order(StringComparer.Ordinal).ToArray();
        rows.Should().Equal(expected.Order(StringComparer.Ordinal));
    }

    [TestMethod]
    public async Task ManyRanges_SameRowsAsInRangePredicate()
    {
        // thousands of nested and disjoint ranges against hundreds of addresses: the indexed lookup returns exactly the
        // pairs ipv4_is_in_range accepts, in source order then lookup-table order
        var rng = new Random(20261021);
        var ranges = Enumerable.Range(0, 1500).Select(i =>
            $"\"{rng.Next(1, 20)}.{rng.Next(0, 4)}.{rng.Next(0, 256)}.{rng.Next(0, 256)}/{rng.Next(8, 33)}\", {i}");
        var addresses = Enumerable.Range(0, 200).Select(i =>
            $"{i}, \"{rng.Next(1, 20)}.{rng.Next(0, 4)}.{rng.Next(0, 256)}.{rng.Next(0, 256)}\"");
        var tables = $"let R = datatable(network:string, Row:long) [{string.Join(",", ranges)}];" +
                     $"let S = datatable(Id:long, ip:string) [{string.Join(",", addresses)}];";

        var lookup = await ResultAsString(tables +
                                          "S | evaluate ipv4_lookup(R, ip, network) | project Id, Row", ";");
        var predicate = await ResultAsString(tables +
                                             "S | extend k = 1 | join kind=inner (R | extend k = 1) on k " +
                                             "| where ipv4_is_in_range(ip, network) | project Id, Row | order by Id asc, Row asc", ";");
        lookup.Split(';').Length.Should().BeGreaterThan(200);
        lookup.Should().Be(predicate);
    }
}
