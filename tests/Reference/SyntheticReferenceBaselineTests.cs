using System.Text.Json;
using OneClick.Core.Formulas;
using OneClick.Core.Geometry;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

/// <summary>
/// Synthetic reference baseline tests using sprinkler-agent-kit/fixtures/reference-network.json.
///
/// Scope: documented synthetic baseline only (21 m route, 19.77 m cut) as stated in
/// sprinkler-agent-kit/CHECKS.md and README. The fixture's declared component counts are
/// design expectations, not independently inferred components, and are NOT asserted here.
/// No manufacturer data, no NFPA/certification claim, no physical part inference.
/// </summary>
public sealed class SyntheticReferenceBaselineTests
{
    private const string FixtureRelativePath = "fixtures/reference-network.json";
    private static readonly string[] SourceRefs = ["fixture:reference-network.json#synthetic-baseline"];

    private sealed record FixturePiece(
        string Id,
        Point3D Start,
        Point3D End,
        double StartNetOffsetM,
        double EndNetOffsetM,
        string Group);

    private sealed record ComputedPiece(
        string Id,
        string Group,
        double RouteLengthM,
        double CutLengthM);

    [Fact]
    [Trait("ValidationEvidence", "SyntheticBaseline")]
    [Trait("ValidationScope", "FixtureOnly")]
    public void SyntheticFixture_BaselineRouteAndCutTotals_MatchDocumented21mAnd19_77m()
    {
        var pieces = LoadFixturePieces();
        var computed = ComputePieces(pieces);

        var routeTotal = computed.Sum(p => p.RouteLengthM);
        var cutTotal = computed.Sum(p => p.CutLengthM);

        // Independent expected values from the documented synthetic baseline (CHECKS.md/README).
        Assert.Equal(21.0d, routeTotal, precision: 10);
        Assert.Equal(19.77d, cutTotal, precision: 10);
    }

    [Fact]
    [Trait("ValidationEvidence", "SyntheticBaseline")]
    [Trait("ValidationScope", "FixtureOnly")]
    public void SyntheticFixture_RouteLengths_UseG01StraightSegmentLengthPerPiece()
    {
        var pieces = LoadFixturePieces();

        foreach (var piece in pieces)
        {
            var route = GeometryFormulas.StraightSegmentLength(piece.Start, piece.End, SourceRefs);

            var known = Assert.IsType<KnownValue>(route);
            Assert.Equal(SiUnit.Meter, known.Unit);
            Assert.Equal("G01.1.0", known.MethodVersion);
            Assert.True(known.Value > 0d, $"Piece {piece.Id} should have positive route length.");

            var expectedRoute = ExpectedRouteLength(piece.Id);
            Assert.Equal(expectedRoute, known.Value, precision: 10);
        }
    }

    [Fact]
    [Trait("ValidationEvidence", "SyntheticBaseline")]
    [Trait("ValidationScope", "FixtureOnly")]
    public void SyntheticFixture_CutTotal_UsesQ01LedgerSeparation_FromRouteAndSyntheticCutLedgers()
    {
        var pieces = LoadFixturePieces();
        var computed = ComputePieces(pieces);

        // Model route and synthetic cut as separate ledgers. This exercises Q01 ledger separation;
        // it does not infer physical parts or manufacturer assemblies.
        var contributions = new List<QuantityContribution>(computed.Count * 2);
        foreach (var p in computed)
        {
            contributions.Add(new QuantityContribution(p.Id, "route", "synthetic-baseline", p.RouteLengthM, SiUnit.Meter));
            contributions.Add(new QuantityContribution(p.Id, "synthetic-cut", "synthetic-baseline", p.CutLengthM, SiUnit.Meter));
        }

        var routeAgg = QuantityFormulas.SumUniqueOwnedContributions(
            contributions, "route", "synthetic-baseline", SiUnit.Meter, SourceRefs);
        var cutAgg = QuantityFormulas.SumUniqueOwnedContributions(
            contributions, "synthetic-cut", "synthetic-baseline", SiUnit.Meter, SourceRefs);

        Assert.Equal(21.0d, routeAgg.Total.Value, precision: 10);
        Assert.Equal(19.77d, cutAgg.Total.Value, precision: 10);
        Assert.Equal(SiUnit.Meter, routeAgg.Total.Unit);
        Assert.Equal(SiUnit.Meter, cutAgg.Total.Unit);
        Assert.Equal("Q01.1.0", routeAgg.Total.MethodVersion);
        Assert.Equal("Q01.1.0", cutAgg.Total.MethodVersion);

        // Ledger separation: same owner IDs appear in different ledgers with different totals.
        Assert.Equal(computed.Count, routeAgg.CountedOwnerIds.Count);
        Assert.Equal(computed.Count, cutAgg.CountedOwnerIds.Count);
        Assert.NotEqual(routeAgg.Total.Value, cutAgg.Total.Value);
    }

    [Fact]
    [Trait("ValidationEvidence", "SyntheticBaseline")]
    [Trait("ValidationScope", "FixtureOnly")]
    public void SyntheticFixture_PieceCount_Is10_ForDocumentedBaselineOnly()
    {
        var pieces = LoadFixturePieces();

        // The fixture declares 10 pipe pieces for this synthetic baseline. This asserts the
        // fixture shape used by the baseline calculation; it is not independent part inference.
        Assert.Equal(10, pieces.Count);
    }

    private static List<ComputedPiece> ComputePieces(IReadOnlyList<FixturePiece> pieces)
    {
        var computed = new List<ComputedPiece>(pieces.Count);
        foreach (var piece in pieces)
        {
            var routeResult = GeometryFormulas.StraightSegmentLength(piece.Start, piece.End, SourceRefs);
            var route = Assert.IsType<KnownValue>(routeResult);

            // Synthetic cut length: route minus declared net end offsets. This is fixture-defined
            // synthetic arithmetic, not a manufacturer takeout claim.
            var cutLength = route.Value - piece.StartNetOffsetM - piece.EndNetOffsetM;

            computed.Add(new ComputedPiece(piece.Id, piece.Group, route.Value, cutLength));
        }

        return computed;
    }

    private static IReadOnlyList<FixturePiece> LoadFixturePieces()
    {
        var path = ResolveFixturePath();
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        // Guard: only use the documented synthetic baseline.
        Assert.Equal("metres", root.GetProperty("units").GetString());

        var piecesEl = root.GetProperty("pieces");
        var pieces = new List<FixturePiece>(piecesEl.GetArrayLength());
        foreach (var p in piecesEl.EnumerateArray())
        {
            var id = p.GetProperty("id").GetString()!;
            var start = ReadPoint3D(p.GetProperty("start_m"));
            var end = ReadPoint3D(p.GetProperty("end_m"));
            var startOffset = p.GetProperty("start_net_offset_m").GetDouble();
            var endOffset = p.GetProperty("end_net_offset_m").GetDouble();
            var group = p.GetProperty("group").GetString()!;
            pieces.Add(new FixturePiece(id, start, end, startOffset, endOffset, group));
        }

        return pieces;
    }

    private static Point3D ReadPoint3D(JsonElement array)
    {
        Assert.Equal(JsonValueKind.Array, array.ValueKind);
        Assert.Equal(3, array.GetArrayLength());
        return new Point3D(
            array[0].GetDouble(),
            array[1].GetDouble(),
            array[2].GetDouble());
    }

    private static string ResolveFixturePath()
    {
        // Primary: copied to test output via csproj Content.
        var outputPath = Path.Combine(AppContext.BaseDirectory, FixtureRelativePath);
        if (File.Exists(outputPath))
            return outputPath;

        // Fallback: walk up from test output to repo root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "sprinkler-agent-kit", "fixtures", "reference-network.json");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(
            $"Could not locate {FixtureRelativePath}. Ensure the fixture is copied to output or present under sprinkler-agent-kit/fixtures.");
    }

    private static double ExpectedRouteLength(string id) => id switch
    {
        "M1" => 3.0d,
        "M2" => 3.0d,
        "M3" => 3.0d,
        "M4" => 3.0d,
        "B1H" => 2.0d,
        "B1D" => 1.0d,
        "B2H" => 2.0d,
        "B2D" => 1.0d,
        "B3H" => 2.0d,
        "B3D" => 1.0d,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unexpected synthetic fixture piece id.")
    };
}
