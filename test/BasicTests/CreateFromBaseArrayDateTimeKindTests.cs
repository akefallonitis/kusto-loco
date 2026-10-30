using AwesomeAssertions;
using KustoLoco.Core.DataSource.Columns;

namespace BasicTests;

/// <summary>
/// The base-array ingest path (<c>NullableSet.CreateFromBaseArray</c>, reached through the public
/// <c>NullableSetLocator.GetNullableForTypeAndBaseArray</c>) turns a .NET <c>DateTime</c> array -- e.g. a
/// column handed over from Parquet or built by a host application -- into an engine column.
///
/// KustoLoco's convention is that a <see cref="DateTimeKind.Unspecified"/> datetime is already a UTC
/// wall-clock: <c>datetime_to_utc</c> relabels Unspecified with <c>SpecifyKind(Utc)</c> (no shift), and
/// <c>KustoFormatter</c> renders an Unspecified value identically to a Utc one. The ingest path, however,
/// applied a blanket <c>.ToUniversalTime()</c>; for an Unspecified value .NET treats that as host-local and
/// shifts it by the host's UTC offset, corrupting the stored instant on any non-UTC host. (CI / the Azure
/// default run at UTC, where local == UTC, which masks the defect.)
///
/// These cells reproduce the defect on a non-UTC host and must hold on EVERY host after the fix
/// (Unspecified -> <c>SpecifyKind(Utc)</c>, Local -> <c>ToUniversalTime()</c>, Utc unchanged).
/// </summary>
[TestClass]
public class CreateFromBaseArrayDateTimeKindTests
{
    // A bare, offset-free wall-clock, exactly what the engine stores as the UTC value.
    private static readonly DateTime UnspecifiedWallClock =
        new(2024, 1, 1, 10, 0, 0, DateTimeKind.Unspecified);

    [TestMethod]
    public void NonNullableUnspecifiedArray_PreservesWallClock()
    {
        // A DateTime[] takes the non-nullable branch of CreateFromBaseArray.
        var input = new[] { UnspecifiedWallClock };

        var set = NullableSetLocator.GetNullableForTypeAndBaseArray(typeof(DateTime), input);

        var stored = (DateTime)set.NullableObject(0)!;
        // Pre-fix on a UTC+NN host this is shifted back by the host offset; the wall-clock must not move.
        stored.Ticks.Should().Be(UnspecifiedWallClock.Ticks);
    }

    [TestMethod]
    public void NullableUnspecifiedArray_PreservesWallClock()
    {
        // A DateTime?[] takes the nullable branch of CreateFromBaseArray.
        var input = new DateTime?[] { UnspecifiedWallClock, null };

        var set = NullableSetLocator.GetNullableForTypeAndBaseArray(typeof(DateTime), input);

        set.IsNull(1).Should().BeTrue();
        var stored = (DateTime)set.NullableObject(0)!;
        stored.Ticks.Should().Be(UnspecifiedWallClock.Ticks);
    }

    [TestMethod]
    public void UtcArray_Unchanged()
    {
        // Regression guard: a genuine UTC value must round-trip unchanged, so the fix does not disturb the
        // already-correct path (ToUniversalTime and SpecifyKind(Utc) are both no-ops on a Utc value).
        var utc = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        var set = NullableSetLocator.GetNullableForTypeAndBaseArray(typeof(DateTime), new[] { utc });

        var stored = (DateTime)set.NullableObject(0)!;
        stored.Ticks.Should().Be(utc.Ticks);
    }
}
