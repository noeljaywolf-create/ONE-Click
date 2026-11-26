using System.Text.Json;
using OneClick.Application.Persistence.InMemory;
using OneClick.Application.Services;
using OneClick.Core.Domain;
using OneClick.Core.Ids;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class SnapshotFreezeExportTests
{
    private const string FixtureRelativePath = "fixtures/reference-network.json";

    [Fact]
    public void Freeze_CapturesRevisionMethodVersionsAndSeparatedQuantities()
    {
        var state = LoadBaselineState();
        state.SetRevision(7);

        var manifest = ProjectSnapshotFreezer.Freeze(
            state,
            snapshotId: "snap-freeze-1",
            createdAtUtc: new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero),
            label: "baseline-freeze");

        Assert.Equal("snap-freeze-1", manifest.SnapshotId);
        Assert.Equal(state.ProjectId.Value, manifest.ProjectId);
        Assert.Equal(7L, manifest.ProjectRevision);
        Assert.Equal("baseline-freeze", manifest.Label);
        Assert.Equal(ProjectSnapshotFreezer.DefaultMethodVersion, manifest.MethodVersion);
        Assert.Contains($"project:{state.ProjectId.Value}", manifest.SourceRefs);
        Assert.Contains("revision:7", manifest.SourceRefs);

        var knownRoute = Assert.Single(manifest.KnownQuantities, q => q.Ledger == ProjectSnapshotFreezer.RouteLedger);
        var knownCut = Assert.Single(manifest.KnownQuantities, q => q.Ledger == ProjectSnapshotFreezer.CutLedger);
        Assert.Equal(SnapshotQuantityStatus.Known, knownRoute.Status);
        Assert.Equal(SnapshotQuantityStatus.Known, knownCut.Status);
        Assert.Equal(21.0d, knownRoute.Value!.Value, precision: 10);
        Assert.Equal(19.77d, knownCut.Value!.Value, precision: 10);
        Assert.Empty(manifest.ProvisionalQuantities);
        Assert.Empty(manifest.UnknownQuantities);
    }

    [Fact]
    public void Freeze_WithUnknownElevation_PreservesUnknownAndDoesNotCoerceToZero()
    {
        var state = LoadBaselineState();
        var piece = state.GetRequiredPiece("B3D");
        var unknown = new UnknownValue(
            SiUnit.Meter,
            ["test:unknown-elevation"],
            "test.v1",
            "Elevation is not known for this slice.",
            ["issue:elevation-unknown:B3D"]);

        state.UpsertPiece(piece.WithElevations(piece.StartElevation, unknown));

        var manifest = ProjectSnapshotFreezer.Freeze(
            state,
            snapshotId: "snap-unknown-1",
            createdAtUtc: new DateTimeOffset(2026, 11, 26, 12, 5, 0, TimeSpan.Zero));

        // Known totals remain separated for known-scope pieces.
        var knownRoute = Assert.Single(manifest.KnownQuantities, q => q.Ledger == ProjectSnapshotFreezer.RouteLedger);
        var knownCut = Assert.Single(manifest.KnownQuantities, q => q.Ledger == ProjectSnapshotFreezer.CutLedger);
        Assert.True(knownRoute.Value.HasValue);
        Assert.True(knownCut.Value.HasValue);
        Assert.True(knownRoute.Value!.Value < 21.0d); // unknown piece excluded from known total

        // Unknown quantities are present, explicit, and not zero.
        var unknownRoute = Assert.Single(manifest.UnknownQuantities, q => q.Ledger == ProjectSnapshotFreezer.RouteLedger && q.GroupKey == "B3D");
        var unknownCut = Assert.Single(manifest.UnknownQuantities, q => q.Ledger == ProjectSnapshotFreezer.CutLedger && q.GroupKey == "B3D");
        Assert.Equal(SnapshotQuantityStatus.Unknown, unknownRoute.Status);
        Assert.Equal(SnapshotQuantityStatus.Unknown, unknownCut.Status);
        Assert.Null(unknownRoute.Value);
        Assert.Null(unknownCut.Value);
        Assert.False(string.IsNullOrWhiteSpace(unknownRoute.Reason));
        Assert.Contains("issue:elevation-unknown:B3D", unknownRoute.IssueRefs);
        Assert.Contains("issue:snapshot:partial-unknown", manifest.Issues);
    }

    [Fact]
    public void ExportJson_And_Csv_IdentifySnapshotAndPreserveUnknownAsNotZero()
    {
        var state = LoadBaselineState();
        var piece = state.GetRequiredPiece("B3D");
        state.UpsertPiece(piece.WithElevations(
            piece.StartElevation,
            new UnknownValue(SiUnit.Meter, ["test:unknown-elevation"], "test.v1", "Elevation is not known.", ["issue:elevation-unknown:B3D"])));

        var manifest = ProjectSnapshotFreezer.Freeze(
            state,
            snapshotId: "snap-export-1",
            createdAtUtc: new DateTimeOffset(2026, 11, 26, 12, 10, 0, TimeSpan.Zero));

        var jsonArtifact = SnapshotExportService.ExportJson(manifest);
        Assert.Equal("application/json", jsonArtifact.ContentType);
        Assert.Contains("\"snapshotId\": \"snap-export-1\"", jsonArtifact.Content);
        Assert.Contains("\"projectRevision\": 0", jsonArtifact.Content);
        Assert.Contains("\"unknownQuantities\"", jsonArtifact.Content);
        // Unknown value must be null in JSON, never 0 for unknown lines.
        using (var doc = JsonDocument.Parse(jsonArtifact.Content))
        {
            var unknowns = doc.RootElement.GetProperty("unknownQuantities");
            Assert.True(unknowns.GetArrayLength() > 0);
            foreach (var u in unknowns.EnumerateArray())
            {
                Assert.Equal("Unknown", u.GetProperty("status").GetString());
                Assert.Equal(JsonValueKind.Null, u.GetProperty("value").ValueKind);
            }
        }

        var csvArtifact = SnapshotExportService.ExportCsv(manifest);
        Assert.Equal("text/csv", csvArtifact.ContentType);
        Assert.Contains("snap-export-1", csvArtifact.Content);
        Assert.Contains("projectRevision", csvArtifact.Content);
        Assert.Contains(",Unknown,", csvArtifact.Content);
        // CSV unknown line should have empty value field, not 0.
        var unknownCsvLines = csvArtifact.Content.Split('\n')
            .Where(l => l.Contains(",Unknown,", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(unknownCsvLines);
        foreach (var line in unknownCsvLines)
        {
            // columns: ledger,groupKey,unit,status,value,...
            var cols = line.Split(',');
            Assert.True(cols.Length >= 5);
            Assert.Equal("Unknown", cols[3]);
            Assert.True(string.IsNullOrEmpty(cols[4])); // value empty, not 0
        }
    }

    [Fact]
    public async Task FreezeService_StoresImmutableManifestInDomainState()
    {
        var store = new InMemoryProjectDomainStateStore();
        var state = LoadBaselineState();
        await store.SaveAsync(state);

        var service = new SnapshotFreezeService(store);
        var manifest = await service.FreezeAsync(
            state.ProjectId,
            snapshotId: "snap-store-1",
            createdAtUtc: new DateTimeOffset(2026, 11, 26, 12, 15, 0, TimeSpan.Zero),
            label: "stored");

        var reloaded = await store.GetAsync(state.ProjectId);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.SnapshotManifests);
        Assert.Equal("snap-store-1", reloaded.SnapshotManifests[0].SnapshotId);
        Assert.Equal(manifest.ProjectRevision, reloaded.SnapshotManifests[0].ProjectRevision);
        Assert.Equal(21.0d, reloaded.SnapshotManifests[0].KnownQuantities.Single(q => q.Ledger == ProjectSnapshotFreezer.RouteLedger).Value);
    }

    private static ProjectDomainState LoadBaselineState()
    {
        var path = ResolveFixturePath();
        return SyntheticReferenceNetworkLoader.LoadBaselineFromFile(path, new ProjectId("project:snapshot-baseline"));
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

        throw new FileNotFoundException($"Could not locate {FixtureRelativePath}.");
    }
}
