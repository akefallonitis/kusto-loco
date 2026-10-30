using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     The element an mv-apply expands from a dynamic array is a dynamic value, whatever type Kusto.Language
///     inferred for the array (a dynamic literal is typed by its shape), so the subquery can
///     reference it. https://learn.microsoft.com/kusto/query/mv-apply-operator
///     Every expected value was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class MvApplyDynamicElementTests : TestMethods
{
    [TestMethod]
    [DataRow("datatable(u:string)[\"x\",\"y\"] | extend d = dynamic([{\"t\":\"a\"},{\"t\":\"b\"}]) " +
             "| mv-apply e = d on (project n = tostring(e.t)) | project u, n", "x,a;x,b;y,a;y,b")]
    [DataRow("datatable(u:string)[\"x\",\"y\"] | extend d = dynamic([{\"t\":\"a\"},{\"t\":\"b\"}]) " +
             "| mv-apply e = d on (where tostring(e.t) == \"a\") | project u, n = tostring(e.t)", "x,a;y,a")]
    [DataRow("datatable(u:string)[\"x\"] | extend d = dynamic([\"a\",\"b\"]) " +
             "| mv-apply e = d on (project n = strcat(tostring(e), \"!\")) | project u, n", "x,a!;x,b!")]
    [DataRow("datatable(u:string)[\"x\"] | extend d = dynamic([1, 2, 3]) " +
             "| mv-apply e = d on (summarize s = sum(tolong(e))) | project u, s", "x,6")]
    [DataRow("datatable(u:string)[\"x\"] | extend d = dynamic({\"k\":[{\"t\":\"a\"}]}) " +
             "| mv-apply e = d.k on (project n = tostring(e.t)) | project u, n", "x,a")]
    public async Task SubqueryReadsTheExpandedElement(string query, string expected)
    {
        (await ResultAsString(query, ";")).Should().Be(expected);
    }
}
