//
// Licensed under the MIT License.

using System;
using System.Threading;

namespace KustoLoco.Core.Evaluation;

/// <summary>
/// The row budget of a query's row-multiplying operators — <c>mv-expand</c> and <c>mv-apply</c> emit one row per array element,
/// so a few input rows holding large arrays can materialise far more rows than the input. A host that bounds the memory an
/// evaluation may take sets <see cref="KustoQueryContext.MaxExpandedRows"/>; every row those operators emit across the whole
/// query is charged here, and the evaluation stops with <see cref="ExpansionBudgetExceededException"/> once past the budget —
/// before the rows past it are built. Unset (null), nothing is charged and nothing changes. Like <see cref="QueryClock"/>, the
/// budget is fixed for one evaluation, including the materialisation of its result.
/// </summary>
public static class ExpansionBudget
{
    private static readonly AsyncLocal<Counter?> Current = new();

    /// <summary>Fixes the budget to <paramref name="maxRows"/> (null: unlimited) until the returned scope is disposed.</summary>
    public static IDisposable Fix(long? maxRows)
    {
        var previous = Current.Value;
        Current.Value = maxRows is { } max ? new Counter(max) : null;
        return new Scope(previous);
    }

    /// <summary>The evaluation's budget, read once per chunk by an expanding operator (null: unlimited).</summary>
    internal static Counter? Budget => Current.Value;

    internal sealed class Counter(long max)
    {
        private long _emitted;

        /// <summary>Charges <paramref name="rows"/> emitted rows; past the budget the evaluation stops.</summary>
        public void Charge(long rows)
        {
            var emitted = Interlocked.Add(ref _emitted, rows);
            if (emitted > max) throw new ExpansionBudgetExceededException(max, emitted);
        }
    }

    private sealed class Scope(Counter? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}

/// <summary>The query's row-multiplying operators would emit more rows than <see cref="KustoQueryContext.MaxExpandedRows"/>
/// allows; the evaluation stopped before building the rows past the budget.</summary>
public sealed class ExpansionBudgetExceededException(long budget, long emitted)
    : Exception($"mv-expand / mv-apply would emit more than {budget:N0} rows (at least {emitted:N0}): the expansion row budget is exceeded")
{
    /// <summary>The budget (<see cref="KustoQueryContext.MaxExpandedRows"/>).</summary>
    public long Budget { get; } = budget;

    /// <summary>The rows charged when the budget was crossed (a lower bound on what the full expansion would emit).</summary>
    public long Emitted { get; } = emitted;
}
