using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     ADX has no null string: a missing string value is the empty string, and make_set,
///     make_list and their _if / _with_nulls forms keep it like any other value.
///     https://learn.microsoft.com/kusto/query/scalar-data-types/null-values
///     https://learn.microsoft.com/kusto/query/make-set-aggregation-function
///     https://learn.microsoft.com/kusto/query/make-list-aggregation-function
///     Every expected value here was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class MakeSetListEmptyStringTests : TestMethods
{
    private async Task<string> Squashed(string query) => Squash(await ResultAsString(query));

    [TestMethod]
    public async Task EmptyStringFromDynamicNullIsKept()
    {
        var result = await Squashed(
            "print v = tostring(dynamic(null)) | summarize s=make_set(v), l=make_list(v), n=count()");
        result.Should().Be("""[""],[""],1""");
    }

    [TestMethod]
    public async Task EmptyStringsInAColumnAreKept()
    {
        var result = await Squashed(
            """datatable(v:string)["", "a", ""] | summarize s=make_set(v), l=make_list(v), dc=dcount(v)""");
        result.Should().Be("""["","a"],["","a",""],2""");
    }

    [TestMethod]
    public async Task IfFormsAndWithNullsKeepTheEmptyString()
    {
        var result = await Squashed(
            "print v = tostring(dynamic(null)) | summarize s=make_set_if(v, true), l=make_list_if(v, true), lw=make_list_with_nulls(v)");
        result.Should().Be("""[""],[""],[""]""");
    }

    [TestMethod]
    public async Task EmptyStringCountsTowardsMaxSize()
    {
        var result = await Squashed(
            """datatable(v:string, k:long)["", 1, "a", 2, "", 3, "b", 4] | summarize s=make_set(v, 2), l=make_list(v, 2), si=make_set_if(v, k > 1), li=make_list_if(v, k > 1)""");
        result.Should().Be("""["","a"],["","a"],["a","","b"],["a","","b"]""");
    }

    [TestMethod]
    public async Task UnionFillsAMissingStringColumnWithTheEmptyString()
    {
        var result = await Squashed(
            """union (print a="x"), (print b=1) | summarize s=make_set(a), l=make_list(a), lw=make_list_with_nulls(a), si=make_set_if(a, true), li=make_list_if(a, true), dc=dcount(a), c=count(), ce=countif(isempty(a)), cn=countif(isnull(a))""");
        result.Should().Be("""["x",""],["x",""],["x",""],["x",""],["x",""],2,2,1,0""");
    }

    [TestMethod]
    public async Task MissingAndEmptyStringsGroupTogether()
    {
        var result = await ResultAsString(
            """union (print a="x"), (print b=1), (print a="") | summarize c=count() by a | order by a asc""");
        result.Should().Be(",2,x,1");
    }

    [TestMethod]
    public async Task MissingAndEmptyStringsAreOneDistinctValue()
    {
        var result = await ResultAsString(
            """union (print a="x"), (print b=1), (print a="") | distinct a | order by a asc""");
        result.Should().Be(",x");
    }

    [TestMethod]
    public async Task MakeBagKeepsAnEmptyStringValue()
    {
        var result = await Squashed(
            """print v = tostring(dynamic(null)) | summarize b=make_bag(bag_pack("k", v)), dc=dcount(v), c=countif(isempty(v)), ce=countif(v == ""), ci=countif(isnull(v))""");
        result.Should().Be("""{"k":""},1,1,1,0""");
    }
}
