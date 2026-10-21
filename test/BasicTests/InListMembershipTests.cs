using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     in / in~ / !in / !in~ over every kind of list element ADX accepts: scalars, dynamic arrays (inline or
///     let-bound), let-bound scalars and tabular expressions; and a dynamic left side.
///     has_any / has_all over let-bound values and dynamic arrays.
///     https://learn.microsoft.com/kusto/query/in-operator
///     https://learn.microsoft.com/kusto/query/in-cs-operator
///     https://learn.microsoft.com/kusto/query/has-anyoperator
///     Every expected value here was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class InListMembershipTests : TestMethods
{
    private const string Tables = """
                                  let T = datatable(Id:long, User:string, Port:long, Props:string) [
                                  1, "alice", 443, '{"role":"admin","level":3,"tags":["a","b"]}',
                                  2, "Bob", 22, '{"role":"user","level":1,"tags":["c"]}',
                                  3, "carol", 8080, '{"role":"Admin","level":2,"tags":[]}'
                                  ];
                                  let L = datatable(Name:string) ["alice", "carol"];
                                  let S = datatable(Id:long, Cmd:string) [
                                  1, "Invoke-Mimikatz -Command",
                                  2, "powershell.exe -enc AAAA",
                                  3, "notepad.exe"
                                  ];
                                  """;

    [TestMethod]
    // scalar lists
    [DataRow("T | where User in ('alice', 'bob') | project Id", "1")]
    [DataRow("T | where User in~ ('alice', 'bob') | project Id", "1,2")]
    [DataRow("T | where User !in ('alice') | project Id", "2,3")]
    [DataRow("T | where User !in~ ('ALICE') | project Id", "2,3")]
    [DataRow("T | where Port in (22, 443) | project Id", "1,2")]
    [DataRow("T | where Port !in (22) | project Id", "1,3")]
    // a let-bound scalar
    [DataRow("let u = 'Bob'; T | where User in (u) | project Id", "2")]
    // dynamic arrays, inline and let-bound
    [DataRow("""T | where User in (dynamic(["alice", "carol"])) | project Id""", "1,3")]
    [DataRow("""let names = dynamic(["alice", "carol"]); T | where User in (names) | project Id""", "1,3")]
    [DataRow("""let names = dynamic(["alice", "carol"]); T | where User !in (names) | project Id""", "2")]
    [DataRow("""let names = dynamic(["ALICE", "Carol"]); T | where User in~ (names) | project Id""", "1,3")]
    [DataRow("let ports = dynamic([22, 8080]); T | where Port in (ports) | project Id", "2,3")]
    // tabular lists
    [DataRow("T | where User in (L | project Name) | project Id", "1,3")]
    [DataRow("T | where User !in (L) | project Id", "2")]
    // a dynamic left side compares as the list's type
    [DataRow("T | extend d = parse_json(Props) | where d.role in ('admin') | project Id", "1")]
    [DataRow("T | extend d = parse_json(Props) | where d.role in~ ('admin') | project Id", "1,3")]
    [DataRow("T | extend d = parse_json(Props) | where d.role !in ('admin', 'Admin') | project Id", "2")]
    [DataRow("T | extend d = parse_json(Props) | where d.level in ('2') | project Id", "3")]
    [DataRow("T | extend d = parse_json(Props) | where tolong(d.level) in (2, 3) | project Id", "1,3")]
    public async Task In_Membership(string query, string expected)
    {
        var result = await ResultAsString(Tables + query);
        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("""let kw = dynamic(["mimikatz", "enc"]); S | where Cmd has_any (kw) | project Id""", "1,2")]
    [DataRow("let kw = 'powershell'; S | where Cmd has_any (kw, 'notepad') | project Id", "2,3")]
    [DataRow("""let kw = dynamic(["powershell", "enc"]); S | where Cmd has_all (kw) | project Id""", "2")]
    public async Task HasAnyAll_LetBoundAndDynamicLists(string query, string expected)
    {
        var result = await ResultAsString(Tables + query);
        result.Should().Be(expected);
    }

    [TestMethod]
    public async Task In_NullNumberIsNotAMember()
    {
        // a null long / real / datetime on the left of in is false, and of !in true
        var query = """
                    datatable(Id:long, V:long, R:real, D:datetime) [
                    1, long(null), real(null), datetime(null),
                    2, 5, 5.0, datetime(2020-01-01)
                    ]
                    | project Id, i = V in (5, 6), n = V !in (5, 6), ri = R in (5.0), rn = R !in (5.0),
                      di = D in (datetime(2020-01-01)), dn = D !in (datetime(2020-01-01))
                    """;
        var result = await ResultAsString(query, ";");
        result.Should().Be("1,False,True,False,True,False,True;2,True,False,True,False,True,False");
    }

    [TestMethod]
    public async Task HasAny_ComputedListIsRefused()
    {
        // previously the non-literal element was silently dropped
        var result = await CreateContext().RunQuery(Tables + "S | where Cmd has_any (strcat('a', 'b')) | project Id");
        result.Error.Should().Contain("has_any over a computed or tabular list");
    }
}
