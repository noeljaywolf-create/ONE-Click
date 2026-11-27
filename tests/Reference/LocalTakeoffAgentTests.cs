using OneClick.Application.Agents;
using OneClick.Application.Agents.Takeoff;
using OneClick.Application.Commands;
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
    public void RegisterTakeoffAgents_RegistersDeterministicAgents()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);

        var descriptors = registry.GetDescriptors();

        Assert.Equal(6, descriptors.Count);
        Assert.All(descriptors, d => Assert.Equal(AgentKind.Deterministic, d.Kind));
        Assert.Contains(descriptors, d => d.Id.Value == "takeoff.baseline.load");
        Assert.Contains(descriptors, d => d.Id.Value == "takeoff.ledger.calculate");
        Assert.Contains(descriptors, d => d.Id.Value == "takeoff.snapshot.freeze");
        Assert.Contains(descriptors, d => d.Id.Value == "takeoff.document.import");
        Assert.Contains(descriptors, d => d.Id.Value == "takeoff.calibration.set");
        Assert.Contains(descriptors, d => d.Id.Value == "takeoff.validation.run");
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

    [Fact]
    public async Task ValidationAgent_ReportsDeterministicFindings_WithCoverageAndLimitations()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:agent-validation");
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
                "Elevation unknown at end of B3D for validation test.",
                ["issue:elevation-unknown:B3D:end"])
        });
        await store.SaveAsync(state);

        var result = await registry.ExecuteAsync<ValidationInput, ValidationOutput>(
            new AgentId("takeoff.validation.run"),
            AgentRequest<ValidationInput>.From(new ValidationInput(projectId)),
            context);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<ValidationOutput>(result.Output);
        Assert.Equal(projectId, output.ProjectId);
        Assert.Equal("all", output.Scope);
        Assert.Equal(10, output.Counts.PiecesInProject);
        Assert.Equal(10, output.Counts.PiecesChecked);
        Assert.True(output.Counts.IssueCount > 0);
        Assert.True(output.Counts.UnknownQuantityCount > 0);
        Assert.Contains(output.Findings, f => f.Code == "validation.elevation.unknown" && f.Scope == "piece:B3D:end-elevation");
        Assert.Contains(output.Findings, f => f.Code == "validation.ledger.quantity_unknown");
        Assert.Contains(output.Coverage, c => c.Contains("RouteLedgerCalculator", StringComparison.Ordinal));
        Assert.Contains(output.Limitations, l => l.Contains("not NFPA/regulatory certification", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(output.Limitations, l => l.Contains("No cloud LLM/network calls", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(output.SourceRefs, r => r.StartsWith("project:", StringComparison.Ordinal));
        Assert.Contains(output.SourceRefs, r => r.Contains(RouteLedgerCalculator.RouteMethodVersion, StringComparison.Ordinal));
        Assert.Contains(result.Issues, i => i == "validation.elevation.unknown");
        Assert.Contains(result.Warnings, w => w.StartsWith("limitation:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidationAgent_MissingProject_ReturnsExplicitFailure()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:missing-validation");
        var context = CreateContext(projectId);

        var result = await registry.ExecuteAsync<ValidationInput, ValidationOutput>(
            new AgentId("takeoff.validation.run"),
            AgentRequest<ValidationInput>.From(new ValidationInput(projectId)),
            context);

        Assert.False(result.IsSuccess);
        Assert.Equal("validation.project.not_found", result.ErrorCode);
    }

    [Fact]
    public async Task ImportDocumentAgent_RegistersMetadataAndRegion_WithProvenance()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:agent-import");
        var context = CreateContext(projectId);
        await store.SaveAsync(new ProjectDomainState(projectId));

        var input = new ImportDocumentInput(
            projectId,
            new CommandId("cmd-import-1"),
            "upload:handle-1",
            "Sheet A1",
            "plan-a1.pdf");

        var result = await registry.ExecuteAsync<ImportDocumentInput, ImportDocumentOutput>(
            new AgentId("takeoff.document.import"),
            AgentRequest<ImportDocumentInput>.From(input),
            context);

        Assert.True(result.IsSuccess, result.ErrorDetail);
        var output = Assert.IsType<ImportDocumentOutput>(result.Output);
        Assert.Equal(projectId, output.ProjectId);
        Assert.Equal(1, output.Revision);
        Assert.Equal("document:cmd-import-1", output.DocumentId);
        Assert.Equal("region:document:cmd-import-1:default", output.RegionId);
        Assert.Contains("document:cmd-import-1", output.CreatedIds);
        Assert.Contains("region:document:cmd-import-1:default", output.CreatedIds);
        Assert.Empty(output.IssueIds);
        Assert.Contains("command:cmd-import-1", output.SourceRefs);
        Assert.Contains("upload:upload:handle-1", output.SourceRefs);
        Assert.Contains(result.ProvenanceRefs, r => r == "method:import-document.v1");
        Assert.Contains(result.ProvenanceRefs, r => r == "project:project:agent-import");

        var saved = await store.GetAsync(projectId);
        Assert.NotNull(saved);
        Assert.Single(saved!.ImportedDocuments);
        Assert.Single(saved.Regions);
        var doc = saved.ImportedDocuments.Single();
        Assert.Equal("upload:handle-1", doc.UploadHandle);
        Assert.Equal("Sheet A1", doc.DocumentLabel);
        Assert.Equal("plan-a1.pdf", doc.SourceFileName);
    }

    [Fact]
    public async Task ImportDocumentAgent_MissingProject_ReturnsTypedFailure()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:missing-import");
        var context = CreateContext(projectId);

        var result = await registry.ExecuteAsync<ImportDocumentInput, ImportDocumentOutput>(
            new AgentId("takeoff.document.import"),
            AgentRequest<ImportDocumentInput>.From(new ImportDocumentInput(
                projectId,
                new CommandId("cmd-import-missing"),
                "upload:handle-missing")),
            context);

        Assert.False(result.IsSuccess);
        Assert.Equal("import-document.project_not_found", result.ErrorCode);
        Assert.Contains("issue:project-not-found:project:missing-import", result.Issues);
    }

    [Fact]
    public async Task SetCalibrationAgent_RegistersCalibrationMetadata_WithProvenance()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:agent-calibration");
        var context = CreateContext(projectId);
        await store.SaveAsync(new ProjectDomainState(projectId));

        var import = await registry.ExecuteAsync<ImportDocumentInput, ImportDocumentOutput>(
            new AgentId("takeoff.document.import"),
            AgentRequest<ImportDocumentInput>.From(new ImportDocumentInput(
                projectId,
                new CommandId("cmd-import-cal"),
                "upload:handle-cal")),
            context);
        Assert.True(import.IsSuccess, import.ErrorDetail);
        var regionId = import.Output!.RegionId;

        var result = await registry.ExecuteAsync<SetCalibrationInput, SetCalibrationOutput>(
            new AgentId("takeoff.calibration.set"),
            AgentRequest<SetCalibrationInput>.From(new SetCalibrationInput(
                projectId,
                new CommandId("cmd-cal-1"),
                regionId,
                [
                    new CalibrationControlPoint(0, 0, 0, 0, null),
                    new CalibrationControlPoint(100, 0, 10, 0, null)
                ],
                "scale check")),
            context);

        Assert.True(result.IsSuccess, result.ErrorDetail);
        var output = Assert.IsType<SetCalibrationOutput>(result.Output);
        Assert.Equal(projectId, output.ProjectId);
        Assert.Equal(2, output.Revision);
        Assert.Equal($"calibration:{regionId}", output.CalibrationId);
        Assert.Equal(regionId, output.RegionId);
        Assert.Contains(output.CalibrationId, output.CreatedIds);
        Assert.Empty(output.InvalidatedIds);
        Assert.Empty(output.IssueIds);
        Assert.Contains(result.ProvenanceRefs, r => r == "method:set-calibration.v1");
        Assert.Contains(result.ProvenanceRefs, r => r == $"region:{regionId}");

        var saved = await store.GetAsync(projectId);
        Assert.NotNull(saved);
        Assert.Single(saved!.Calibrations);
        var calibration = saved.Calibrations.Single();
        Assert.Equal($"calibration:{regionId}", calibration.CalibrationId);
        Assert.Equal(regionId, calibration.RegionId);
        Assert.Equal(2, calibration.ControlPoints.Count);
        Assert.Equal("scale check", calibration.Notes);
    }

    [Fact]
    public async Task SetCalibrationAgent_MissingRegion_ReturnsTypedFailure()
    {
        var store = new InMemoryProjectDomainStateStore();
        var registry = TakeoffAgentRegistration.CreateTakeoffRegistry(store);
        var projectId = new ProjectId("project:agent-calibration-missing-region");
        var context = CreateContext(projectId);
        await store.SaveAsync(new ProjectDomainState(projectId));

        var result = await registry.ExecuteAsync<SetCalibrationInput, SetCalibrationOutput>(
            new AgentId("takeoff.calibration.set"),
            AgentRequest<SetCalibrationInput>.From(new SetCalibrationInput(
                projectId,
                new CommandId("cmd-cal-missing"),
                "region:missing",
                [
                    new CalibrationControlPoint(0, 0, 0, 0, null),
                    new CalibrationControlPoint(1, 1, 1, 1, null)
                ])),
            context);

        Assert.False(result.IsSuccess);
        Assert.Equal("set-calibration.rejected", result.ErrorCode);
        Assert.Contains("issue:set-calibration:region-not-found:region:missing", result.Issues);
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
