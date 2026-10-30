using System;
using System.Globalization;
using System.Text.Json.Nodes;
using KustoLoco.Core.Util;

namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

[KustoImplementation(Keyword = "Functions.ToString")]
internal partial class ToStringFunction
{
    private static string DecImpl(decimal input) => input.ToString(CultureInfo.InvariantCulture);
    private static string IntImpl(int input) => input.ToString(CultureInfo.InvariantCulture);
    private static string LongImpl(long input) => input.ToString(CultureInfo.InvariantCulture);
    private static string DoubleImpl(double input) => input.ToString(CultureInfo.InvariantCulture);
    private static string StringImpl(string input) => input;
    private static string GuidImpl(string input) => input.ToString(CultureInfo.InvariantCulture);
    private static string DtImpl(DateTime input) => input.ToString("M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
    private static string TsImpl(TimeSpan input) => input.ToString();

    // a string value is returned as it is, null as the empty string, and anything else as
    // compact JSON with ADX escaping
    private static string DynImpl(JsonNode input) => DynamicText.ToKustoString(input);
}
