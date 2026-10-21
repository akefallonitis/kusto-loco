// The instant now() and ago() read.
// https://learn.microsoft.com/kusto/query/now-function , https://learn.microsoft.com/kusto/query/ago-function

using System;
using System.Threading;

namespace KustoLoco.Core.Evaluation;

/// <summary>
/// The instant now() and ago() read. A query evaluation reads its context's clock ONCE and every now()/ago() call in that
/// evaluation sees that one value — ADX fixes now() for the duration of a query. Outside an evaluation scope (an engine
/// driven directly) the wall clock is read.
/// </summary>
public static class QueryClock
{
    private static readonly AsyncLocal<DateTime?> Current = new();

    /// <summary>The evaluation's instant (UTC).</summary>
    public static DateTime UtcNow => Current.Value ?? DateTime.UtcNow;

    /// <summary>Fixes <see cref="UtcNow"/> to <paramref name="utcNow"/> until the returned scope is disposed.</summary>
    public static IDisposable Fix(DateTime utcNow)
    {
        var previous = Current.Value;
        Current.Value = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow.ToUniversalTime(), DateTimeKind.Utc);
        return new Scope(previous);
    }

    private sealed class Scope(DateTime? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
