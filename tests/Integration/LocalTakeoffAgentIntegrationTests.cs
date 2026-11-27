using OneClick.Application.Agents;
using OneClick.Application.Agents.Audit;
using OneClick.Application.Agents.Takeoff;
using OneClick.Application.Contracts;
using OneClick.Application.Persistence.InMemory;
using OneClick.Core.Domain;
using OneClick.Core.Ids;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Tests.Integration;

/// <summary>
/// Integration tests for local deterministic takeoff agent composition and execution.
/// Covers agent registration via TakeoffAgentRegistration/ILocalAgentRegistry, execution of
/// baseline load, ledger calculation, and snapshot freeze agents through the registry, using
/// synthetic fixture data only. No Desktop UI, no network/AI calls.
///
/// Run-record capture: no run-record store/wrapper integration is present in the current
/// Application agent framework; these tests verify composition/execution behavior and surface
/// that gap in test output/reporting rather than faking capture.
/// </summary>
public sealed class LocalTakeoffAgentIntegrationTests
{
    private const string FixtureRelativePath = "fixtures/reference-network.json";

    [Fact]
    public void RegisterTakeoffAgents_RegistersExpectedDeterministicAgents()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = new InMemoryLocalAgentRegistry();

        TakeoffAgentRegistration.RegisterTakeoffAgents(registry, store);

        var descriptors = registry.GetDescriptors();
        Assert.Equal(6, descriptors.Count);
        Assert.All(descriptors, d => Assert.Equal(AgentKind.Deterministic, d.Kind));

        AssertDescriptor(descriptors, "takeoff.baseline.load", "Baseline Load");
        AssertDescriptor(descriptors, "takeoff.ledger.calculate", "Ledger Calculation");
        AssertDescriptor(descriptors, "takeoff.snapshot.freeze", "Snapshot Freeze");
        AssertDescriptor(descriptors, "takeoff.document.import", "Import Document");
        AssertDescriptor(descriptors, "takeoff.calibration.set", "Set Calibration");
        AssertDescriptor(descriptors, "takeoff.validation.run", "Takeoff Validation");

        Assert.True(registry.TryGet(new AgentId("takeoff.baseline.load"), out var baseline));
        Assert.True(registry.TryGet(new AgentId("takeoff.ledger.calculate"), out var ledger));
        Assert.True(registry.TryGet(new AgentId("takeoff.snapshot.freeze"), out var freeze));
        Assert.True(registry.TryGet(new AgentId("takeoff.document.import"), out var importDoc));
        Assert.True(registry.TryGet(new AgentId("takeoff.calibration.set"), out var calibration));
        Assert.True(registry.TryGet(new AgentId("takeoff.validation.run"), out var validation));
        Assert.NotNull(baseline);
        Assert.NotNull(ledger);
        Assert.NotNull(freeze);
        Assert.NotNull(importDoc);
        Assert.NotNull(calibration);
        Assert.NotNull(validation);
    }

    [Fact]
    public void CreateTakeoffRegistry_ComposesInMemoryRegistryWithAgents()
    {
        var store = new InMemoryProjectDomainStateStore();

        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);

        Assert.IsType<InMemoryLocalAgentRegistry>(registry);
        Assert.Equal(6, registry.GetDescriptors().Count);
    }

    [Fact]
    public void RegisterTakeoffAgents_DuplicateRegistration_FailsExplicitly()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = new InMemoryLocalAgentRegistry();
        registry.RegisterTakeoffAgents(store);

        var ex = Assert.Throws<InvalidOperationException>(() => registry.RegisterTakeoffAgents(store));

        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Registry_Execute_ReturnsNotFoundForUnregisteredAgent()
    {
        var registry = new InMemoryLocalAgentRegistry();
        var context = CreateContext(new ProjectId("project:missing-agent"));

        var result = await registry.ExecuteAsync<LedgerCalculationInput, LedgerCalculationOutput>(
            new AgentId("takeoff.missing"),
            AgentRequest<LedgerCalculationInput>.From(new LedgerCalculationInput(new ProjectId("project:missing-agent"))),
            context);

        Assert.False(result.IsSuccess);
        Assert.Equal("agent.not_found", result.ErrorCode);
        Assert.Contains("not registered", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BaselineLoadAgent_ThroughRegistry_LoadsSyntheticFixtureAndPersistsDomainState()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:integration-baseline");
        var context = CreateContext(projectId);
        var fixturePath = ResolveFixturePath();

        var result = await registry.ExecuteAsync<BaselineLoadInput, BaselineLoadOutput>(
            new AgentId("takeoff.baseline.load"),
            AgentRequest<BaselineLoadInput>.From(new BaselineLoadInput(projectId, fixturePath)),
            context);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal(projectId, result.Output!.ProjectId);
        Assert.Equal(10, result.Output.PieceCount);
        Assert.Equal(Path.GetFullPath(fixturePath), result.Output.FixturePath);
        Assert.Contains(SyntheticReferenceNetworkLoader.FixtureSourceRef, result.Output.SourceRefs);
        Assert.Contains(result.ProvenanceRefs, r => r.StartsWith("project:", StringComparison.Ordinal));
        Assert.Contains(result.ProvenanceRefs, r => r.StartsWith("fixture:", StringComparison.Ordinal));
        Assert.Contains(result.ProvenanceRefs, r => r.Contains(SyntheticReferenceNetworkLoader.DefaultMethodVersion, StringComparison.Ordinal));

        var snapshot = await store.GetSnapshotAsync(projectId);
        Assert.NotNull(snapshot);
        Assert.Equal(10, snapshot!.Pieces.Count);
        Assert.Equal(result.Output.Revision, snapshot.Revision);
    }

    [Fact]
    public async Task LedgerCalculationAgent_ThroughRegistry_ComputesDeterministicLedgersWithProvenance()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:integration-ledger");
        var context = CreateContext(projectId);

        await LoadBaselineAsync(registry, projectId, context);

        var result = await registry.ExecuteAsync<LedgerCalculationInput, LedgerCalculationOutput>(
            new AgentId("takeoff.ledger.calculate"),
            AgentRequest<LedgerCalculationInput>.From(new LedgerCalculationInput(projectId)),
            context);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<LedgerCalculationOutput>(result.Output);
        Assert.Equal(projectId, output.ProjectId);
        Assert.Equal(10, output.PieceCount);
        Assert.Equal(10, output.Ledgers.Pieces.Count);

        var routeTotal = Assert.IsType<KnownValue>(output.Ledgers.RouteTotal);
        var cutTotal = Assert.IsType<KnownValue>(output.Ledgers.CutTotal);
        Assert.Equal(21.0d, routeTotal.Value, precision: 10);
        Assert.Equal(19.77d, cutTotal.Value, precision: 10);

        var b3d = output.Ledgers.Pieces.Single(p => p.PieceId == "B3D");
        var b3dRoute = Assert.IsType<KnownValue>(b3d.RouteLength);
        var b3dCut = Assert.IsType<KnownValue>(b3d.CutLength);
        Assert.Equal(1.0d, b3dRoute.Value, precision: 10);
        Assert.Equal(0.925d, b3dCut.Value, precision: 10);

        Assert.Empty(result.Issues);
        Assert.Contains(result.ProvenanceRefs, r => r.Contains(RouteLedgerCalculator.RouteMethodId, StringComparison.Ordinal));
        Assert.Contains(result.ProvenanceRefs, r => r.Contains(RouteLedgerCalculator.CutMethodId, StringComparison.Ordinal));
        Assert.Contains(result.ProvenanceRefs, r => r.Contains(RouteLedgerCalculator.RouteMethodVersion, StringComparison.Ordinal));
        Assert.Contains(result.ProvenanceRefs, r => r.Contains(RouteLedgerCalculator.CutMethodVersion, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SnapshotFreezeAgent_ThroughRegistry_FreezesExportsAndPreservesUnknownAsUnknown()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:integration-snapshot");
        var context = CreateContext(projectId);

        await LoadBaselineAsync(registry, projectId, context);

        var state = await store.GetAsync(projectId);
        Assert.NotNull(state);
        var b3d = state!.GetRequiredPiece("B3D");
        state.UpsertPiece(b3d with
        {
            EndElevation = new UnknownValue(
                SiUnit.Meter,
                b3d.SourceRefs,
                b3d.MethodVersion,
                "Elevation unknown at end of B3D for deterministic integration test.",
                ["issue:elevation-unknown:B3D:end"])
        });
        await store.SaveAsync(state);

        var freezeInput = new SnapshotFreezeInput(
            projectId,
            "snap-integration-1",
            new DateTimeOffset(2026, 11, 27, 0, 0, 0, TimeSpan.Zero),
            Label: "integration-snapshot",
            Exclusions: null,
            ExportJson: true,
            ExportCsv: true);

        var result = await registry.ExecuteAsync<SnapshotFreezeInput, SnapshotFreezeOutput>(
            new AgentId("takeoff.snapshot.freeze"),
            AgentRequest<SnapshotFreezeInput>.From(freezeInput),
            context);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<SnapshotFreezeOutput>(result.Output);
        Assert.Equal("snap-integration-1", output.SnapshotId);
        Assert.Equal(projectId, output.ProjectId);
        Assert.NotNull(output.Manifest);
        Assert.NotNull(output.JsonExport);
        Assert.NotNull(output.CsvExport);

        var unknownRoute = Assert.Single(output.Manifest.UnknownQuantities, q => q.GroupKey == "B3D" && q.Ledger == RouteLedgerCalculator.RouteLedgerName);
        var unknownCut = Assert.Single(output.Manifest.UnknownQuantities, q => q.GroupKey == "B3D" && q.Ledger == RouteLedgerCalculator.CutLedgerName);
        Assert.Null(unknownRoute.Value);
        Assert.Null(unknownCut.Value);
        Assert.Equal(SnapshotQuantityStatus.Unknown, unknownRoute.Status);
        Assert.Equal(SnapshotQuantityStatus.Unknown, unknownCut.Status);

        var knownRoute = output.Manifest.KnownQuantities.Single(q => q.Ledger == RouteLedgerCalculator.RouteLedgerName);
        var knownCut = output.Manifest.KnownQuantities.Single(q => q.Ledger == RouteLedgerCalculator.CutLedgerName);
        Assert.Equal(20.0d, knownRoute.Value!.Value, precision: 10);
        Assert.Equal(18.845d, knownCut.Value!.Value, precision: 10);

        Assert.Contains("\"value\": null", output.JsonExport!.Content, StringComparison.Ordinal);
        Assert.Contains("snap-integration-1", output.CsvExport!.Content, StringComparison.Ordinal);
        Assert.Contains(result.Issues, i => i.Contains("elevation-unknown", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("unknown quantities", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ProvenanceRefs, r => r.StartsWith("snapshot:", StringComparison.Ordinal));
        Assert.Contains(result.ProvenanceRefs, r => r.StartsWith("project:", StringComparison.Ordinal));

        var persisted = await store.GetAsync(projectId);
        Assert.NotNull(persisted);
        Assert.Contains(persisted!.SnapshotManifests, m => m.SnapshotId == "snap-integration-1");
    }

    [Fact]
    public async Task ValidationAgent_ThroughRegistry_ReportsIssuesCoverageAndLimitations()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:integration-validation");
        var context = CreateContext(projectId);

        await LoadBaselineAsync(registry, projectId, context);

        var state = await store.GetAsync(projectId);
        Assert.NotNull(state);
        var b3d = state!.GetRequiredPiece("B3D");
        state.UpsertPiece(b3d with
        {
            EndElevation = new UnknownValue(
                SiUnit.Meter,
                b3d.SourceRefs,
                b3d.MethodVersion,
                "Elevation unknown at end of B3D for validation integration test.",
                ["issue:elevation-unknown:B3D:end"])
        });
        await store.SaveAsync(state);

        var result = await registry.ExecuteAsync<ValidationInput, ValidationOutput>(
            new AgentId("takeoff.validation.run"),
            AgentRequest<ValidationInput>.From(new ValidationInput(projectId)),
            context);

        Assert.True(result.IsSuccess, result.ErrorDetail);
        var output = Assert.IsType<ValidationOutput>(result.Output);
        Assert.Equal(10, output.Counts.PiecesChecked);
        Assert.True(output.Counts.IssueCount > 0);
        Assert.True(output.Counts.UnknownQuantityCount > 0);
        Assert.Contains(output.Findings, f => f.Severity == ValidationFindingSeverity.Issue);
        Assert.Contains(output.Coverage, c => c.Contains("RouteLedgerCalculator", StringComparison.Ordinal));
        Assert.Contains(output.Limitations, l => l.Contains("not NFPA/regulatory certification", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(output.Limitations, l => l.Contains("No cloud LLM/network calls", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(output.SourceRefs, r => r.StartsWith("project:", StringComparison.Ordinal));
        Assert.Contains(result.Issues, i => i == "validation.elevation.unknown");
    }

    [Fact]
    public async Task ComposedRegistryFlow_BaselineLedgerSnapshot_IsDeterministicForSameInputs()
    {
        var projectId = new ProjectId("project:integration-deterministic");
        var createdAt = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

        var first = await RunComposedFlowAsync(projectId, "snap-deterministic-a", createdAt);
        var second = await RunComposedFlowAsync(projectId, "snap-deterministic-a", createdAt);

        AssertTaggedValueEqual(first.Ledger.RouteTotal, second.Ledger.RouteTotal);
        AssertTaggedValueEqual(first.Ledger.CutTotal, second.Ledger.CutTotal);
        Assert.Equal(first.Json, second.Json);
        Assert.Equal(first.Csv, second.Csv);
        Assert.Equal(first.Manifest.MethodVersion, second.Manifest.MethodVersion);
        Assert.True(first.Manifest.Issues.SequenceEqual(second.Manifest.Issues));
        Assert.True(first.Manifest.SourceRefs.SequenceEqual(second.Manifest.SourceRefs));
        Assert.Equal(first.Manifest.KnownQuantities.Count, second.Manifest.KnownQuantities.Count);
        Assert.Equal(first.Manifest.UnknownQuantities.Count, second.Manifest.UnknownQuantities.Count);
        Assert.Equal(first.Manifest.ProvisionalQuantities.Count, second.Manifest.ProvisionalQuantities.Count);
    }

    private static async Task<(ProjectRouteLedgers Ledger, SnapshotManifest Manifest, string Json, string Csv)> RunComposedFlowAsync(
        ProjectId projectId,
        string snapshotId,
        DateTimeOffset createdAt)
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var context = CreateContext(projectId);

        await LoadBaselineAsync(registry, projectId, context);

        var ledgerResult = await registry.ExecuteAsync<LedgerCalculationInput, LedgerCalculationOutput>(
            new AgentId("takeoff.ledger.calculate"),
            AgentRequest<LedgerCalculationInput>.From(new LedgerCalculationInput(projectId)),
            context);
        Assert.True(ledgerResult.IsSuccess);

        var freezeResult = await registry.ExecuteAsync<SnapshotFreezeInput, SnapshotFreezeOutput>(
            new AgentId("takeoff.snapshot.freeze"),
            AgentRequest<SnapshotFreezeInput>.From(new SnapshotFreezeInput(projectId, snapshotId, createdAt, "deterministic", ExportJson: true, ExportCsv: true)),
            context);
        Assert.True(freezeResult.IsSuccess);

        var output = freezeResult.Output!;
        return (ledgerResult.Output!.Ledgers, output.Manifest, output.JsonExport!.Content, output.CsvExport!.Content);
    }

    private static async Task LoadBaselineAsync(ILocalAgentRegistry registry, ProjectId projectId, AgentRunContext context)
    {
        var loadResult = await registry.ExecuteAsync<BaselineLoadInput, BaselineLoadOutput>(
            new AgentId("takeoff.baseline.load"),
            AgentRequest<BaselineLoadInput>.From(new BaselineLoadInput(projectId, ResolveFixturePath())),
            context);
        Assert.True(loadResult.IsSuccess, loadResult.ErrorDetail);
    }

    [Fact]
    public async Task Registry_WithRunRecordStore_CapturesDeterministicRunRecords()
    {
        var store = new InMemoryProjectDomainStateStore();
        var runRecords = new InMemoryAgentRunRecordStore();
        var registry = new InMemoryLocalAgentRegistry(runRecords);
        registry.RegisterTakeoffAgents(store);

        var projectId = new ProjectId("project:integration-run-records");
        var context = CreateContext(projectId);

        await LoadBaselineAsync(registry, projectId, context);

        var ledgerResult = await registry.ExecuteAsync<LedgerCalculationInput, LedgerCalculationOutput>(
            new AgentId("takeoff.ledger.calculate"),
            AgentRequest<LedgerCalculationInput>.From(new LedgerCalculationInput(projectId)),
            context);
        Assert.True(ledgerResult.IsSuccess);

        var records = await runRecords.GetByProjectAsync(projectId);
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal(AgentRunStatus.Succeeded, r.Status));
        Assert.All(records, r => Assert.Equal(projectId, r.ProjectId));
        Assert.All(records, r => Assert.Equal(context.CorrelationId, r.CorrelationId));
        Assert.Contains(records, r => r.AgentId.Value == "takeoff.baseline.load");
        Assert.Contains(records, r => r.AgentId.Value == "takeoff.ledger.calculate");
        Assert.All(records, r => Assert.False(string.IsNullOrWhiteSpace(r.AgentVersion)));
    }

    private static void AssertDescriptor(IReadOnlyList<AgentDescriptor> descriptors, string id, string name)
    {
        var descriptor = Assert.Single(descriptors, d => d.Id.Value == id);
        Assert.Equal(name, descriptor.Name);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description));
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Version));
    }

    private static void AssertTaggedValueEqual(TaggedValue expected, TaggedValue actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        switch (expected, actual)
        {
            case (KnownValue ek, KnownValue ak):
                Assert.Equal(ek.Unit, ak.Unit);
                Assert.Equal(ek.Value, ak.Value);
                Assert.Equal(ek.MethodVersion, ak.MethodVersion);
                Assert.True(ek.SourceRefs.SequenceEqual(ak.SourceRefs));
                break;
            case (UnknownValue eu, UnknownValue au):
                Assert.Equal(eu.Unit, au.Unit);
                Assert.Equal(eu.MethodVersion, au.MethodVersion);
                Assert.Equal(eu.Reason, au.Reason);
                Assert.True(eu.SourceRefs.SequenceEqual(au.SourceRefs));
                Assert.True(eu.IssueRefs.SequenceEqual(au.IssueRefs));
                break;
            case (ProvisionalValue ep, ProvisionalValue ap):
                Assert.Equal(ep.Unit, ap.Unit);
                Assert.Equal(ep.Value, ap.Value);
                Assert.Equal(ep.MethodVersion, ap.MethodVersion);
                Assert.True(ep.AssumptionRefs.SequenceEqual(ap.AssumptionRefs));
                Assert.True(ep.SourceRefs.SequenceEqual(ap.SourceRefs));
                Assert.True(ep.IssueRefs.SequenceEqual(ap.IssueRefs));
                break;
            default:
                Assert.Equal(expected, actual);
                break;
        }
    }

    private static AgentRunContext CreateContext(ProjectId projectId) =>
        new(projectId, new ActorContext("integration-test-actor", "integration-test-session"), "corr-integration", CancellationToken.None);

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

        throw new FileNotFoundException(
            $"Could not locate {FixtureRelativePath}. Ensure the fixture is present under sprinkler-agent-kit/fixtures.");
    }
}
