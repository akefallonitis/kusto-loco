using System.Globalization;
using AwesomeAssertions;

namespace BasicTests;

/// <summary>
/// ADX datetime literals are UTC. Kusto.Language parses a <c>datetime(...Z)</c> literal into the
/// host's LOCAL frame, so on a non-UTC host the engine mis-represents an absolute UTC literal by the
/// local offset: a rule's <c>where T &gt;= datetime(&lt;ISO-Z&gt;)</c> window then shifts and silently
/// drops rows (missed detections). UTC hosts (CI, Azure default) mask it because local == UTC there.
/// These cells reproduce the defect on a non-UTC host and must hold on EVERY host after the fix
/// (normalize datetime literals to UTC in IRTranslator.VisitLiteralExpression).
/// </summary>
[TestClass]
public class DatetimeLiteralUtcTests : TestMethods
{
    public DatetimeLiteralUtcTests()
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-GB");
    }

    [TestMethod]
    public async Task UtcZLiteral_RendersAsUtc()
    {
        // The engine renders datetime columns with a 'Z' (UTC) suffix; a UTC literal must round-trip
        // unshifted. Pre-fix on a non-UTC host this renders the local-shifted wall clock.
        (await LastLineOfResult("print datetime(2024-01-01T10:00:00Z)"))
            .Should().Be("2024-01-01 10:00:00Z");
    }

    [TestMethod]
    public async Task UtcZLiteral_EqualsBareLiteral_SameInstant()
    {
        // datetime(...Z) and the equivalent bare literal denote the same UTC instant -> compare equal.
        (await LastLineOfResult(
                "print eq = (datetime(2024-01-01T10:00:00Z) == datetime(2024-01-01 10:00:00))"))
            .Should().Be("True");
    }

    [TestMethod]
    public async Task UtcZFilter_IncludesBoundaryRow()
    {
        // A UTC-valued column (bare literal, no offset) vs a '...Z' filter literal: the boundary row
        // must be kept. Pre-fix on a non-UTC host the filter literal shifts forward and excludes it.
        var query = """
                    datatable(T:datetime)[ datetime(2024-01-01 10:00:00) ]
                    | where T >= datetime(2024-01-01T10:00:00Z)
                    | count
                    """;
        (await LastLineOfResult(query)).Should().Be("1");
    }

    [TestMethod]
    public async Task BareLiteralFilter_Unbroken()
    {
        // Regression guard: an unspecified-offset literal's wall clock must NOT move under the fix
        // (Unspecified -> SpecifyKind(Utc) keeps the ticks; only Local -> ToUniversalTime recovers UTC).
        var query = """
                    datatable(T:datetime)[ datetime(2024-01-01 10:00:00) ]
                    | where T >= datetime(2024-01-01 10:00:00)
                    | count
                    """;
        (await LastLineOfResult(query)).Should().Be("1");
    }
}
