using System;

// ReSharper disable PartialTypeWithSinglePart
namespace KustoLoco.Core.Evaluation.BuiltIns.Impl;

// InitialValue: the result for two null operands, which the partitioned evaluation starts from
[KustoImplementation(Keyword = "Operators.Equal", InitialValue = "null")]
internal partial class EqualFunction
{
    private static bool? IntImpl(int? a, int? b) =>
        a.HasValue && b.HasValue ? a.Value == b.Value : NullComparison.Equal(a.HasValue, b.HasValue);
    private static bool? LongImpl(long? a, long? b) =>
        a.HasValue && b.HasValue ? a.Value == b.Value : NullComparison.Equal(a.HasValue, b.HasValue);

    private static bool? DoubleImpl(double? a, double? b) =>
        a.HasValue && b.HasValue ? a.Value == b.Value : NullComparison.Equal(a.HasValue, b.HasValue);
    private static bool? DecimalImpl(decimal? a, decimal? b) =>
        a.HasValue && b.HasValue ? a.Value == b.Value : NullComparison.Equal(a.HasValue, b.HasValue);
    private static bool? TsImpl(TimeSpan? a, TimeSpan? b) =>
        a.HasValue && b.HasValue ? a.Value == b.Value : NullComparison.Equal(a.HasValue, b.HasValue);
    private static bool? DtImpl(DateTime? a, DateTime? b) =>
        a.HasValue && b.HasValue ? a.Value == b.Value : NullComparison.Equal(a.HasValue, b.HasValue);
    private static bool StrImpl(string a, string b) => a == b;
    private static bool? BoolImpl(bool? a, bool? b) =>
        a.HasValue && b.HasValue ? a.Value == b.Value : NullComparison.Equal(a.HasValue, b.HasValue);
    private static bool? GuilImpl(Guid? a, Guid? b) =>
        a.HasValue && b.HasValue ? a.Value == b.Value : NullComparison.Equal(a.HasValue, b.HasValue);
}
