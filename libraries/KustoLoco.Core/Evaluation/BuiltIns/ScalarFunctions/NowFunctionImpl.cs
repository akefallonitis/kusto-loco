using System;
using System.Diagnostics;
using Kusto.Language.Symbols;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

internal class NowFunctionImpl : IScalarFunctionImpl
{
    // reads the query clock; now(offset) adds the offset: https://learn.microsoft.com/kusto/query/now-function
    public ScalarResult InvokeScalar(ScalarResult[] arguments)
    {
        MyDebug.Assert(arguments.Length <= 1);
        var now = QueryClock.UtcNow;
        if (arguments.Length == 1)
            return new ScalarResult(ScalarTypes.DateTime, arguments[0].Value is TimeSpan offset ? now + offset : null);
        return new ScalarResult(ScalarTypes.DateTime, now);
    }
    //now() is never called in columnar because it takes no arguments
    //and we shortcut to creating singlevalue column; now(offset) is, with a per-row offset
    public ColumnarResult InvokeColumnar(ColumnarResult[] arguments)
    {
        if (arguments.Length != 1) throw new NotSupportedException();
        var now = QueryClock.UtcNow;
        var offsets = arguments[0].Column;
        var data = new object?[offsets.RowCount];
        for (var i = 0; i < data.Length; i++)
            data[i] = offsets.GetRawDataValue(i) is TimeSpan offset ? now + offset : null;
        return new ColumnarResult(KustoLoco.Core.Util.ColumnHelpers.CreateFromObjectArray(data, ScalarTypes.DateTime));
    }
}
