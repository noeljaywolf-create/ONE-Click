using OneClick.Application.Agents;
using OneClick.Application.Agents.Takeoff;
using OneClick.Application.Contracts;
using OneClick.Application.Persistence.InMemory;
using OneClick.Core.Domain;
using OneClick.Core.Ids;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class LocalTakeoffAgentTests
{
    private const string FixtureRelativePath = "fixtures/reference-network.json";

    [Fact]
    public void RegisterTakeoffAgents_RegistersThreeDeterministicAgents()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);

        var descriptors = registry.GetDescriptors();

        Assert.Equal(3, descriptors.Count);
        Assert.All(descriptors, d => Assert.Equal(AgentKind.Deterministic, d.Kind));
        Assert.Contains(descriptors, d => d.Id.Value == "takeoff.baseline.load");
        Assert.Contains(descriptors, d => d.Id.Value == "takeoff.ledger.calculate");
        Assert.Contains(descriptors, d => d.Id.Value == "takeoff.snapshot.freeze");
    }

    [Fact]
    public async Task BaselineLoadAgent_LoadsFixtureIntoDomainState_WithProvenance()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:agent-baseline");
        var context = CreateContext(projectId);

        var result = await registry.ExecuteAsync<BaselineLoadInput, BaselineLoadOutput>(
            new AgentId("takeoff.baseline.load"),
            AgentRequest<BaselineLoadInput>.From(new BaselineLoadInput(projectId, ResolveFixturePath())),
            context);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal(projectId, result.Output!.ProjectId);
        Assert.Equal(10, result.Output.PieceCount);
        Assert.Contains(result.ProvenanceRefs, r => r.StartsWith("project:", StringComparison.Ordinal));
        Assert.Contains(result.ProvenanceRefs, r => r.StartsWith("fixture:", StringComparison.Ordinal));

        var saved = await store.GetSnapshotAsync(projectId);
        Assert.NotNull(saved);
        Assert.Equal(10, saved!.Pieces.Count);
    }

    [Fact]
    public async Task LedgerCalculationAgent_ComputesLedgers_WithIssuesAndProvenance()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:agent-ledger");
        var context = CreateContext(projectId);

        await registry.ExecuteAsync<BaselineLoadInput, BaselineLoadOutput>(
            new AgentId("takeoff.baseline.load"),
            AgentRequest<BaselineLoadInput>.From(new BaselineLoadInput(projectId, ResolveFixturePath())),
            context);

        var result = await registry.ExecuteAsync<LedgerCalculationInput, LedgerCalculationOutput>(
            new AgentId("takeoff.ledger.calculate"),
            AgentRequest<LedgerCalculationInput>.From(new LedgerCalculationInput(projectId)),
            context);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        var ledgers = result.Output!.Ledgers;
        Assert.Equal(10, ledgers.Pieces.Count);

        var routeTotal = Assert.IsType<KnownValue>(ledgers.RouteTotal);
        var cutTotal = Assert.IsType<KnownValue>(ledgers.CutTotal);
        Assert.Equal(21.0d, routeTotal.Value, precision: 10);
        Assert.Equal(19.77d, cutTotal.Value, precision: 10);

        var b3d = ledgers.Pieces.Single(p => p.PieceId == "B3D");
        Assert.IsType<KnownValue>(b3d.RouteLength);
        Assert.IsType<KnownValue>(b3d.CutLength);
        Assert.Contains(result.ProvenanceRefs, r => r.Contains(RouteLedgerCalculator.RouteMethodVersion, StringComparison.Ordinal));
        Assert.Contains(result.ProvenanceRefs, r => r.Contains(RouteLedgerCalculator.CutMethodVersion, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SnapshotFreezeAgent_FreezesAndOptionallyExports_WithUnknownPreserved()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:agent-snapshot");
        var context = CreateContext(projectId);

        await registry.ExecuteAsync<BaselineLoadInput, BaselineLoadOutput>(
            new AgentId("takeoff.baseline.load"),
            AgentRequest<BaselineLoadInput>.From(new BaselineLoadInput(projectId, ResolveFixturePath())),
            context);

        var state = await store.GetAsync(projectId);
        Assert.NotNull(state);
        var b3d = state!.GetRequiredPiece("B3D");
        state.UpsertPiece(b3d with
        {
            EndElevation = new UnknownValue(
                SiUnit.Meter,
                b3d.SourceRefs,
                b3d.MethodVersion,
                "Elevation unknown at end of B3D for agent test.",
                ["issue:elevation-unknown:B3D:end"])
        });
        await store.SaveAsync(state);

        var result = await registry.ExecuteAsync<SnapshotFreezeInput, SnapshotFreezeOutput>(
            new AgentId("takeoff.snapshot.freeze"),
            AgentRequest<SnapshotFreezeInput>.From(new SnapshotFreezeInput(
                projectId,
                "snap-agent-1",
                new DateTimeOffset(2026, 11, 27, 0, 0, 0, TimeSpan.Zero),
                Label: "agent-test",
                ExportJson: true,
                ExportCsv: true)),
            context);

        Assert.True(result.IsSuccess);
        var output = result.Output!;
        Assert.NotNull(output.Manifest);
        Assert.NotNull(output.JsonExport);
        Assert.NotNull(output.CsvExport);
        Assert.Equal("snap-agent-1", output.Manifest.SnapshotId);

        var unknownRoute = Assert.Single(output.Manifest.UnknownQuantities, q => q.GroupKey == "B3D" && q.Ledger == RouteLedgerCalculator.RouteLedgerName);
        Assert.Null(unknownRoute.Value);
        Assert.Equal(SnapshotQuantityStatus.Unknown, unknownRoute.Status);

        Assert.Contains("\"value\": null", output.JsonExport!.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("\"value\": 0", output.JsonExport.Content, StringComparison.Ordinal); // coarse guard; unknown must be null
        Assert.Contains(result.ProvenanceRefs, r => r.StartsWith("snapshot:", StringComparison.Ordinal));
    }

    private static AgentRunContext CreateContext(ProjectId projectId) =>
        new(projectId, new ActorContext("test-actor", "test-session"), "corr-test", CancellationToken.None);

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
