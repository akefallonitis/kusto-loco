//
// Licensed under the MIT License.
// ReSharper disable PartialTypeWithSinglePart
using System;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

[KustoImplementation(Keyword = "Functions.Ago")]
internal partial class AgoFunction
{
    // reads the query clock: https://learn.microsoft.com/kusto/query/ago-function
    private static DateTime Impl(TimeSpan t) => QueryClock.UtcNow - t;
}