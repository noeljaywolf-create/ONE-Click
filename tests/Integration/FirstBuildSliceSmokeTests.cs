using System.Text.Json;
using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Persistence.InMemory;
using OneClick.Application.Services;
using OneClick.Core.Domain;
using OneClick.Core.Ids;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Tests.Integration;

public sealed class FirstBuildSliceSmokeTests
{
    private const string FixtureRelativePath = "fixtures/reference-network.json";

    [Fact]
    public async Task Bootstrap_Command_Unknown_Freeze_Export_SmokeFlow()
    {
        // 1) Load synthetic baseline through the same composition as DesktopProjectBootstrapper/domain store.
        var projectId = new ProjectId("project:synthetic-baseline");
        var actor = new ActorContext("desktop-user", "desktop-session");
        var domainStore = new InMemoryProjectDomainStateStore();
        var commandService = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        commandService.CreateProject(projectId);

        var fixturePath = ResolveFixturePath();
        var baseline = SyntheticReferenceNetworkLoader.LoadBaselineFromFile(fixturePath, projectId);
        await domainStore.SaveAsync(baseline);

        var loadedState = await domainStore.GetAsync(projectId);
        Assert.NotNull(loadedState);
        Assert.Equal(10, loadedState!.Pieces.Count);
        Assert.Empty(loadedState.SnapshotManifests);

        var projectView = await commandService.QueryProjectAsync(projectId, actor);
        Assert.Equal(ProjectRevision.Initial, projectView.Revision);

        // Baseline sanity: B3D elevations known.
        var b3d = loadedState.GetRequiredPiece("B3D");
        Assert.IsType<KnownValue>(b3d.StartElevation);
        Assert.IsType<KnownValue>(b3d.EndElevation);

        // 2) Change one elevation via UpsertRouteCommand, make elevation unknown.
        // B3D baseline: start [9,2,3], end [9,2,2]. Make end elevation unknown (Z=null).
        var upsert = new UpsertRouteCommand(
            RouteId: "B3D",
            Vertices:
            [
                new RouteVertex(9, 2, 3),
                new RouteVertex(9, 2, null)
            ],
            StartPortId: null,
            EndPortId: null,
            RouteNotes: "smoke:make-end-elevation-unknown");

        var receipt = await commandService.ExecuteCommandAsync(
            projectId,
            projectView.Revision,
            new CommandId("cmd-smoke-upsert-b3d"),
            upsert,
            actor);

        Assert.Equal(new ProjectRevision(1), receipt.NewRevision);
        Assert.Contains(receipt.InvalidatedIds, id => id == "piece:B3D");
        Assert.Contains(receipt.InvalidatedIds, id => id == "ledger:route:B3D");
        Assert.Contains(receipt.InvalidatedIds, id => id == "ledger:cut:B3D");
        Assert.Contains(receipt.IssueIds, id => id.Contains("elevation-unknown", StringComparison.OrdinalIgnoreCase));

        var stateAfter = await domainStore.GetAsync(projectId);
        Assert.NotNull(stateAfter);
        var b3dAfter = stateAfter!.GetRequiredPiece("B3D");
        Assert.IsType<KnownValue>(b3dAfter.StartElevation);
        Assert.IsType<UnknownValue>(b3dAfter.EndElevation);

        // 3) Compute/verify route & cut ledgers separate and unknown not zero.
        var preFreezeManifest = ProjectSnapshotFreezer.Freeze(
            stateAfter,
            snapshotId: "snap-smoke-preview",
            createdAtUtc: new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero),
            label: "smoke-preview");

        var knownRoute = preFreezeManifest.KnownQuantities.Single(q => q.Ledger == ProjectSnapshotFreezer.RouteLedger);
        var knownCut = preFreezeManifest.KnownQuantities.Single(q => q.Ledger == ProjectSnapshotFreezer.CutLedger);
        Assert.Equal(SnapshotQuantityStatus.Known, knownRoute.Status);
        Assert.Equal(SnapshotQuantityStatus.Known, knownCut.Status);
        Assert.Equal(SiUnit.Meter.ToString(), knownRoute.Unit);
        Assert.Equal(SiUnit.Meter.ToString(), knownCut.Unit);
        // B3D excluded from known totals; baseline 21.0 route, 19.77 cut; B3D is 1.0/0.925.
        Assert.Equal(20.0d, knownRoute.Value!.Value, precision: 10);
        Assert.Equal(18.845d, knownCut.Value!.Value, precision: 10);

        var unknownRoute = preFreezeManifest.UnknownQuantities.Single(q => q.Ledger == ProjectSnapshotFreezer.RouteLedger && q.GroupKey == "B3D");
        var unknownCut = preFreezeManifest.UnknownQuantities.Single(q => q.Ledger == ProjectSnapshotFreezer.CutLedger && q.GroupKey == "B3D");
        Assert.Equal(SnapshotQuantityStatus.Unknown, unknownRoute.Status);
        Assert.Equal(SnapshotQuantityStatus.Unknown, unknownCut.Status);
        Assert.Null(unknownRoute.Value); // unknown not zero
        Assert.Null(unknownCut.Value);   // unknown not zero
        Assert.NotEqual(0d, knownRoute.Value!.Value);
        Assert.False(string.IsNullOrWhiteSpace(unknownRoute.Reason));
        Assert.False(string.IsNullOrWhiteSpace(unknownCut.Reason));
        Assert.Contains("issue:snapshot:partial-unknown", preFreezeManifest.Issues);

        // 4) Freeze snapshot via SnapshotFreezeService.
        var freezeService = new SnapshotFreezeService(domainStore);
        var frozen = await freezeService.FreezeAsync(
            projectId,
            snapshotId: "snap-smoke-frozen",
            createdAtUtc: new DateTimeOffset(2026, 11, 27, 12, 5, 0, TimeSpan.Zero),
            label: "smoke-frozen");

        Assert.Equal("snap-smoke-frozen", frozen.SnapshotId);
        Assert.Equal(projectId.Value, frozen.ProjectId);
        Assert.True(frozen.ProjectRevision >= receipt.NewRevision.Value);
        Assert.Equal(preFreezeManifest.KnownQuantities.Count, frozen.KnownQuantities.Count);
        Assert.Equal(preFreezeManifest.UnknownQuantities.Count, frozen.UnknownQuantities.Count);

        var stateWithManifest = await domainStore.GetAsync(projectId);
        Assert.NotNull(stateWithManifest);
        Assert.Contains(stateWithManifest!.SnapshotManifests, m => m.SnapshotId == "snap-smoke-frozen");

        // 5) Export JSON/CSV via SnapshotExportService and verify unknown remains unknown (null/empty, not 0).
        var json = SnapshotExportService.ExportJson(frozen);
        Assert.Equal("application/json", json.ContentType);
        Assert.Contains("snap-smoke-frozen", json.Content);

        using (var doc = JsonDocument.Parse(json.Content))
        {
            var root = doc.RootElement;
            Assert.Equal("snap-smoke-frozen", root.GetProperty("snapshotId").GetString());

            var unknowns = root.GetProperty("unknownQuantities");
            Assert.True(unknowns.GetArrayLength() >= 2);
            foreach (var u in unknowns.EnumerateArray())
            {
                Assert.Equal("Unknown", u.GetProperty("status").GetString());
                Assert.Equal(JsonValueKind.Null, u.GetProperty("value").ValueKind);
            }

            var knowns = root.GetProperty("knownQuantities");
            Assert.Contains(knowns.EnumerateArray(), k =>
                k.GetProperty("ledger").GetString() == ProjectSnapshotFreezer.RouteLedger
                && k.GetProperty("status").GetString() == "Known"
                && Math.Abs(k.GetProperty("value").GetDouble() - 20.0d) < 1e-9);
            Assert.Contains(knowns.EnumerateArray(), k =>
                k.GetProperty("ledger").GetString() == ProjectSnapshotFreezer.CutLedger
                && k.GetProperty("status").GetString() == "Known"
                && Math.Abs(k.GetProperty("value").GetDouble() - 18.845d) < 1e-9);
        }

        var csv = SnapshotExportService.ExportCsv(frozen);
        Assert.Equal("text/csv", csv.ContentType);
        Assert.Contains("snap-smoke-frozen", csv.Content);

        var lines = csv.Content.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var unknownLines = lines.Where(l => l.Contains(",Unknown,", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(unknownLines);
        foreach (var line in unknownLines)
        {
            var cols = SplitCsvLine(line);
            // ledger,groupKey,unit,status,value,...
            Assert.True(cols.Count >= 5, $"Unexpected CSV shape: {line}");
            Assert.Equal("Unknown", cols[3]);
            Assert.True(string.IsNullOrEmpty(cols[4]), $"Unknown value must be empty, not '{cols[4]}'. Line: {line}");
            Assert.NotEqual("0", cols[4]);
        }

        // Known totals present and not conflated.
        var unit = SiUnit.Meter.ToString();
        Assert.Contains(lines, l => l.StartsWith($"route,synthetic-baseline,{unit},Known,20", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith($"synthetic-cut,synthetic-baseline,{unit},Known,18.845", StringComparison.Ordinal));
    }

    private static string ResolveFixturePath()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, FixtureRelativePath);
        if (File.Exists(outputPath))
            return outputPath;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "sprinkler-agent-kit", "fixtures", "reference-network.json");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Could not locate {FixtureRelativePath}.");
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == ',')
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        result.Add(sb.ToString());
        return result;
    }
}
