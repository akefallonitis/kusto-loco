using AwesomeAssertions;

namespace BasicTests;

/// <summary>
///     An mv-apply subquery may begin with any tabular operator, including the ones that pass the expanded
///     rows' columns through by name: extend, serialize and the project-away / project-keep / project-rename /
///     project-reorder family.
///     https://learn.microsoft.com/kusto/query/mv-apply-operator
///     Every expected value was checked against the ADX engine (kustainer).
/// </summary>
[TestClass]
public class MvApplyLeadingOperatorTests : TestMethods
{
    private const string Resources =
        "datatable(TargetResources:dynamic, K:long)[dynamic([{\"type\":\"User\"},{\"type\":\"Role\"}]), 7] ";

    private const string Numbers = "datatable(A:dynamic, K:long)[dynamic([1,2,3]), 7] ";

    [TestMethod]
    [DataRow(Resources + "| mv-apply P = TargetResources on (extend N = tostring(P.type)) | project K, N",
        "7,User;7,Role")]
    [DataRow(Resources + "| mv-apply TargetResources on (extend N = tostring(TargetResources.type)) | project K, N",
        "7,User;7,Role")]
    [DataRow("datatable(A:dynamic, K:long)[dynamic([\"a\",\"b\"]), 7] " +
             "| mv-apply P = A to typeof(string) on (extend N = strcat(P, 'x')) | project K, N",
        "7,ax;7,bx")]
    [DataRow(Numbers + "| mv-apply A to typeof(long) on (extend D = A * 2) | project K, A, D",
        "7,1,2;7,2,4;7,3,6")]
    [DataRow("datatable(A:dynamic, K:long)[dynamic([1,2]), 7] " +
             "| mv-apply P = A to typeof(long) on (extend K = K + P) | project K, P",
        "8,1;9,2")]
    [DataRow("datatable(A:dynamic, B:dynamic, K:long)[dynamic([1,2]), dynamic([\"x\",\"y\"]), 7] " +
             "| mv-apply P = A to typeof(long), Q = B to typeof(string) on (extend N = strcat(Q, tostring(P))) " +
             "| project K, N",
        "7,x1;7,y2")]
    [DataRow("datatable(A:dynamic, K:long)[dynamic([1,2,3]), 7, dynamic([10]), 8] " +
             "| mv-apply P = A to typeof(long) on (extend D = P * 2 | summarize S = sum(D)) | project K, S",
        "7,12;8,20")]
    [DataRow(Numbers + "| mv-apply P = A to typeof(long) on (extend D = P * 2 | where D > 2) | project K, D",
        "7,4;7,6")]
    [DataRow("datatable(A:dynamic, K:long)[dynamic([3,1,2]), 7] " +
             "| mv-apply P = A to typeof(long) on (extend D = P * 10 | top 1 by D) | project K, D",
        "7,30")]
    public async Task SubqueryBeginningWithExtend(string query, string expected)
    {
        (await ResultAsString(query, ";")).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(Resources + "| mv-apply P = TargetResources on (serialize R = row_number()) | project K, R", "7,1;7,2")]
    [DataRow(Resources + "| mv-apply P = TargetResources on (project-away K) | project N = tostring(P.type)",
        "User;Role")]
    [DataRow(Resources + "| mv-apply P = TargetResources on (project-keep P) | project N = tostring(P.type)",
        "User;Role")]
    [DataRow(Resources + "| mv-apply P = TargetResources on (project-rename Q = P) | project K, N = tostring(Q.type)",
        "7,User;7,Role")]
    [DataRow(Resources + "| mv-apply P = TargetResources on (project-reorder K) | project K, N = tostring(P.type)",
        "7,User;7,Role")]
    public async Task SubqueryBeginningWithAColumnPassingOperator(string query, string expected)
    {
        (await ResultAsString(query, ";")).Should().Be(expected);
    }
}
