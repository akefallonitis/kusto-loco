using System.Text.Json.Nodes;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

[KustoImplementation(Keyword = "Functions.ArrayConcat")]
internal partial class ArrayConcatFunction
{
    /// <remarks>
    ///     https://learn.microsoft.com/kusto/query/array-concat-function
    ///     array_concat joins its array arguments. Only the single-argument form is implemented
    ///     here, and in ADX that returns the array as it is: array_concat(dynamic([1, [2, 3]]))
    ///     is [1, [2, 3]], and a value that is not an array gives []. (This used to splice the
    ///     elements of nested arrays, which suited make_list over an array column while that
    ///     nested the arrays; make_list now flattens them as ADX does.)
    /// </remarks>
    public JsonNode? Impl(JsonNode node)
        => node is JsonArray array
            ? new JsonArray(JsonArrayHelper.ClonedItems(array))
            : new JsonArray();
}
