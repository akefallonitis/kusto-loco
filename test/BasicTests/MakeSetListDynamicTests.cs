using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     make_set, make_list and their _if / _with_nulls forms over a dynamic column: an array
///     value contributes its elements, any other value contributes itself.
///     https://learn.microsoft.com/kusto/query/make-set-aggregation-function
///     https://learn.microsoft.com/kusto/query/make-list-aggregation-function
///     Every expected value here was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class MakeSetListDynamicTests : TestMethods
{
    private async Task<string> Squashed(string query) => Squash(await ResultAsString(query, "|"));

    [TestMethod]
    public async Task ArrayOfBagsIsFlattened()
    {
        var result = await Squashed(
            """datatable(d:dynamic)[dynamic([{"UPN":"a"}])] | summarize s=make_set(d), l=make_list(d)""");
        result.Should().Be("""[{"UPN":"a"}],[{"UPN":"a"}]""");
    }

    [TestMethod]
    public async Task ArraysScalarsAndBagsMix()
    {
        var result = await Squashed(
            """datatable(d:dynamic)[dynamic([1,2]), dynamic([2,3]), dynamic(4), dynamic({"a":1})] | summarize s=make_set(d), l=make_list(d)""");
        result.Should().Be("""[1,2,3,4,{"a":1}],[1,2,2,3,4,{"a":1}]""");
    }

    [TestMethod]
    public async Task OnlyOneLevelIsFlattenedAndEmptyArraysAddNothing()
    {
        var result = await Squashed(
            """datatable(d:dynamic)[dynamic([1,[2,3]]), dynamic([])] | summarize s=make_set(d), l=make_list(d)""");
        result.Should().Be("""[1,[2,3]],[1,[2,3]]""");
    }

    [TestMethod]
    public async Task ScalarValuesKeepTheirType()
    {
        var result = await Squashed(
            """datatable(d:dynamic)[dynamic("x"), dynamic("x"), dynamic(1), dynamic(1.5)] | summarize s=make_set(d), l=make_list(d)""");
        result.Should().Be("""["x",1,1.5],["x","x",1,1.5]""");
    }

    [TestMethod]
    public async Task SetComparesJsonText()
    {
        // 1 and 1.0, "1" and 1, and bags with the same properties in another order are distinct
        var result = await Squashed(
            """datatable(d:dynamic)[dynamic(1), dynamic(1.0), dynamic("1"), dynamic([1]), dynamic({"a":1,"b":2}), dynamic({"b":2,"a":1}), dynamic(true)] | summarize s=make_set(d)""");
        result.Should().Be("""[1,1.0,"1",{"a":1,"b":2},{"b":2,"a":1},true]""");
    }

    [TestMethod]
    public async Task NullValuesAndNullElementsAreSkipped()
    {
        var result = await Squashed(
            """datatable(s:string)["[1,2]", "", "[null,2,{\"a\":[3]}]", "x"] | extend d = parse_json(s) | summarize st=make_set(d), l=make_list(d)""");
        result.Should().Be("""[1,2,{"a":[3]},"x"],[1,2,2,{"a":[3]},"x"]""");
    }

    [TestMethod]
    public async Task WithNullsKeepsNullValuesAndFlattensArrays()
    {
        var result = await Squashed(
            """datatable(s:string)["[1,2]", "", "[null,2]"] | extend d = parse_json(s) | summarize lw=make_list_with_nulls(d)""");
        result.Should().Be("""[1,2,null,null,2]""");
    }

    [TestMethod]
    public async Task IfFormsFlattenAndCountElementsTowardsTheLimit()
    {
        var result = await Squashed(
            """datatable(d:dynamic, k:long)[dynamic([1,2]), 1, dynamic([2,3]), 2] | summarize s=make_set_if(d, k > 1), l=make_list_if(d, k > 1), s2=make_set_if(d, true, 2), l2=make_list_if(d, true, 3)""");
        result.Should().Be("""[2,3],[2,3],[1,2],[1,2,2]""");
    }

    [TestMethod]
    public async Task SizeLimitCountsElements()
    {
        var result = await Squashed(
            """datatable(d:dynamic)[dynamic([1,2,3]), dynamic([4])] | summarize s=make_set(d, 2), l=make_list(d, 2)""");
        result.Should().Be("""[1,2],[1,2]""");
    }

    [TestMethod]
    public async Task FlattensPerGroup()
    {
        var result = await Squashed(
            """datatable(d:dynamic, g:string)[dynamic([{"UPN":"a"}]), "x", dynamic([{"UPN":"a"},{"UPN":"b"}]), "x", dynamic(["z"]), "y"] | summarize s=make_set(d), l=make_list(d) by g | order by g asc""");
        result.Should().Be("""x,[{"UPN":"a"},{"UPN":"b"}],[{"UPN":"a"},{"UPN":"a"},{"UPN":"b"}]|y,["z"],["z"]""");
    }

    [TestMethod]
    public async Task ArrayConcatOfOneArrayReturnsIt()
    {
        var result = await Squashed(
            """print c = array_concat(dynamic([1,[2,3]])), d = array_concat(dynamic([[1],[2]]))""");
        result.Should().Be("""[1,[2,3]],[[1],[2]]""");
    }
}
