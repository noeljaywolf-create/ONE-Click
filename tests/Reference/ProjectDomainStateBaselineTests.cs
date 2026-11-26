using System.Text.Json;
using OneClick.Core.Domain;
using OneClick.Core.Formulas;
using OneClick.Core.Geometry;
using OneClick.Core.Ids;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

/// <summary>
/// First build slice: minimal in-memory project domain state loaded from synthetic reference fixture.
/// Scope: synthetic baseline only. No manufacturer data, no NFPA/certification claim, no physical part inference.
/// </summary>
public sealed class ProjectDomainStateBaselineTests
{
    private const string FixtureRelativePath = "fixtures/reference-network.json";
    private static readonly string[] SourceRefs = ["fixture:reference-network.json#synthetic-baseline"];

    [Fact]
    [Trait("ValidationEvidence", "SyntheticBaseline")]
    [Trait("ValidationScope", "FixtureOnly")]
    public void LoadBaseline_FromReferenceNetwork_PreservesDocumentedRouteAndCutTotals()
    {
        var state = LoadBaselineState();

        var computed = ComputePieces(state.Pieces);

        var routeTotal = computed.Sum(p => p.RouteLengthM);
        var cutTotal = computed.Sum(p => p.CutLengthM);

        // Documented synthetic baseline: 21 m route, 19.77 m cut. Not manufacturer validation.
        Assert.Equal(21.0d, routeTotal, precision: 10);
        Assert.Equal(19.77d, cutTotal, precision: 10);
        Assert.Equal(10, state.Pieces.Count);
        Assert.Equal(0L, state.Revision);
    }

    [Fact]
    [Trait("ValidationEvidence", "SyntheticBaseline")]
    [Trait("ValidationScope", "FixtureOnly")]
    public void LoadBaseline_PreservesStableIdsGroupsAndGeometry()
    {
        var state = LoadBaselineState();

        var ids = state.Pieces.Select(p => p.Id).ToArray();
        Assert.Equal(new[] { "M1", "M2", "M3", "M4", "B1H", "B1D", "B2H", "B2D", "B3H", "B3D" }, ids);

        foreach (var piece in state.Pieces)
        {
            Assert.False(string.IsNullOrWhiteSpace(piece.Id));
            Assert.False(string.IsNullOrWhiteSpace(piece.Group));
            Assert.Contains(piece.Group, new[] { "main_DN50_synthetic", "branch_DN25_synthetic" });
            Assert.NotEmpty(piece.SourceRefs);
            Assert.False(string.IsNullOrWhiteSpace(piece.MethodVersion));
            Assert.True(piece.StartNetOffsetM >= 0d);
            Assert.True(piece.EndNetOffsetM >= 0d);

            var route = GeometryFormulas.StraightSegmentLength(piece.StartM, piece.EndM, SourceRefs);
            var known = Assert.IsType<KnownValue>(route);
            Assert.Equal(SiUnit.Meter, known.Unit);
            Assert.True(known.Value > 0d);
        }
    }

    [Fact]
    [Trait("ValidationEvidence", "SyntheticBaseline")]
    [Trait("ValidationScope", "FixtureOnly")]
    public void Baseline_RouteAndFabricationCutLedgers_AreDistinct()
    {
        var state = LoadBaselineState();
        var computed = ComputePieces(state.Pieces);

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
        Assert.NotEqual(routeAgg.Total.Value, cutAgg.Total.Value);
        Assert.Equal(computed.Count, routeAgg.CountedOwnerIds.Count);
        Assert.Equal(computed.Count, cutAgg.CountedOwnerIds.Count);
    }

    [Fact]
    public void UnknownElevation_IsPreservedAsUnknown_NotZero()
    {
        var state = LoadBaselineState();
        var piece = state.GetRequiredPiece("B3D");

        var unknown = new UnknownValue(
            SiUnit.Meter,
            ["test:unknown-elevation"],
            "test.v1",
            "Elevation is not known for this slice.",
            ["issue:elevation-unknown:B3D"]);

        var updated = piece.WithElevations(piece.StartElevation, unknown);
        state.UpsertPiece(updated);

        var stored = state.GetRequiredPiece("B3D");
        var endElevation = Assert.IsType<UnknownValue>(stored.EndElevation);
        Assert.Null(endElevation.Value);
        Assert.Contains("issue:elevation-unknown:B3D", endElevation.IssueRefs);

        // Unknown must remain unknown; it must not be coerced into a numeric zero or known value.
    }

    [Fact]
    public void Loader_RejectsDuplicatePieceIds()
    {
        var json = """
        {
          "units": "metres",
          "pieces": [
            { "id": "M1", "start_m": [0,0,0], "end_m": [1,0,0], "start_net_offset_m": 0, "end_net_offset_m": 0, "group": "g" },
            { "id": "M1", "start_m": [1,0,0], "end_m": [2,0,0], "start_net_offset_m": 0, "end_net_offset_m": 0, "group": "g" }
          ]
        }
        """;
        using var doc = JsonDocument.Parse(json);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SyntheticReferenceNetworkLoader.LoadBaseline(doc.RootElement, new ProjectId("proj-1")));

        Assert.Contains("Duplicate piece id", ex.Message);
    }

    [Fact]
    public void ProjectState_SupportsRevisionAndSnapshotManifestPlaceholders()
    {
        var state = LoadBaselineState();
        Assert.Equal(0L, state.Revision);

        state.SetRevision(3);
        Assert.Equal(3L, state.Revision);

        var manifest = new SnapshotManifest(
            snapshotId: "snap-1",
            projectId: state.ProjectId.Value,
            projectRevision: state.Revision,
            createdAtUtc: new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero),
            label: "baseline-placeholder");

        state.AddSnapshotManifest(manifest);

        Assert.Single(state.SnapshotManifests);
        Assert.Equal("snap-1", state.SnapshotManifests[0].SnapshotId);
        Assert.Equal(3L, state.SnapshotManifests[0].ProjectRevision);

        var snapshot = state.ToSnapshot();
        Assert.Equal(state.ProjectId, snapshot.ProjectId);
        Assert.Equal(3L, snapshot.Revision);
        Assert.Equal(10, snapshot.Pieces.Count);
        Assert.Single(snapshot.SnapshotManifests);
    }

    private static ProjectDomainState LoadBaselineState()
    {
        var path = ResolveFixturePath();
        return SyntheticReferenceNetworkLoader.LoadBaselineFromFile(path, new ProjectId("project:synthetic-baseline"));
    }

    private sealed record ComputedPiece(string Id, string Group, double RouteLengthM, double CutLengthM);

    private static List<ComputedPiece> ComputePieces(IEnumerable<RoutePiece> pieces)
    {
        var computed = new List<ComputedPiece>();
        foreach (var piece in pieces)
        {
            var routeResult = GeometryFormulas.StraightSegmentLength(piece.StartM, piece.EndM, SourceRefs);
            var route = Assert.IsType<KnownValue>(routeResult);
            var cutLength = route.Value - piece.StartNetOffsetM - piece.EndNetOffsetM;
            computed.Add(new ComputedPiece(piece.Id, piece.Group, route.Value, cutLength));
        }

        return computed;
    }

    private static string ResolveFixturePath()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, FixtureRelativePath);
        if (File.Exists(outputPath))
            return outputPath;

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
}
