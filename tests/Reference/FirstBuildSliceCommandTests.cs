using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Dispatch;
using OneClick.Application.Persistence.InMemory;
using OneClick.Application.Services;
using OneClick.Core.Domain;
using OneClick.Core.Ids;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class FirstBuildSliceCommandTests
{
    private const string FixtureRelativePath = "fixtures/reference-network.json";

    [Fact]
    [Trait("ValidationEvidence", "SyntheticBaseline")]
    [Trait("ValidationScope", "FixtureOnly")]
    public async Task LoadBaseline_ChangeOneElevation_RouteAndCutLedgersChange()
    {
        var (service, domainStore, projectId, actor) = CreateServiceWithBaseline();

        var before = RouteLedgerCalculator.Compute((await domainStore.GetAsync(projectId))!.Pieces);
        var beforeRoute = Assert.IsType<KnownValue>(before.RouteTotal);
        var beforeCut = Assert.IsType<KnownValue>(before.CutTotal);
        Assert.Equal(21.0d, beforeRoute.Value, precision: 10);
        Assert.Equal(19.77d, beforeCut.Value, precision: 10);

        var m1 = (await domainStore.GetAsync(projectId))!.GetRequiredPiece("M1");

        // Change M1 end elevation by +1.0m; start stays same. This lengthens M1.
        var command = new UpsertRouteCommand(
            RouteId: "M1",
            Vertices:
            [
                new RouteVertex(m1.StartM.X, m1.StartM.Y, m1.StartM.Z),
                new RouteVertex(m1.EndM.X, m1.EndM.Y, m1.EndM.Z + 1.0d),
            ],
            StartPortId: null,
            EndPortId: null,
            RouteNotes: "test elevation change");

        var receipt = await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-elev-1"),
            command,
            actor);

        Assert.Equal(1, receipt.NewRevision.Value);
        Assert.Contains("piece:M1", receipt.InvalidatedIds);
        Assert.Contains("ledger:route:M1", receipt.InvalidatedIds);
        Assert.Contains("ledger:cut:M1", receipt.InvalidatedIds);
        Assert.Empty(receipt.IssueIds);

        var afterState = await domainStore.GetAsync(projectId);
        Assert.NotNull(afterState);
        Assert.Equal(1L, afterState!.Revision);

        var after = RouteLedgerCalculator.Compute(afterState.Pieces);
        var afterRoute = Assert.IsType<KnownValue>(after.RouteTotal);
        var afterCut = Assert.IsType<KnownValue>(after.CutTotal);

        Assert.True(afterRoute.Value > beforeRoute.Value);
        Assert.True(afterCut.Value > beforeCut.Value);

        var m1After = after.Pieces.Single(p => p.PieceId == "M1");
        var m1Route = Assert.IsType<KnownValue>(m1After.RouteLength);
        var m1Cut = Assert.IsType<KnownValue>(m1After.CutLength);
        Assert.True(m1Route.Value > 3.0d); // M1 was 3m horizontal at same elevation
        Assert.Equal(m1Route.Value - m1.StartNetOffsetM - m1.EndNetOffsetM, m1Cut.Value, precision: 10);
    }

    [Fact]
    [Trait("ValidationEvidence", "SyntheticBaseline")]
    [Trait("ValidationScope", "FixtureOnly")]
    public async Task MakeOneElevationUnknown_PropagatesUnknownToAffectedLedgers()
    {
        var (service, domainStore, projectId, actor) = CreateServiceWithBaseline();

        var m1 = (await domainStore.GetAsync(projectId))!.GetRequiredPiece("M1");

        var command = new UpsertRouteCommand(
            RouteId: "M1",
            Vertices:
            [
                new RouteVertex(m1.StartM.X, m1.StartM.Y, null), // unknown start elevation
                new RouteVertex(m1.EndM.X, m1.EndM.Y, m1.EndM.Z),
            ],
            StartPortId: null,
            EndPortId: null,
            RouteNotes: "make start elevation unknown");

        var receipt = await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-unknown-1"),
            command,
            actor);

        Assert.Equal(1, receipt.NewRevision.Value);
        Assert.Contains("issue:elevation-unknown:M1:start", receipt.IssueIds);

        var state = await domainStore.GetAsync(projectId);
        Assert.NotNull(state);

        var updatedPiece = state!.GetRequiredPiece("M1");
        var startElev = Assert.IsType<UnknownValue>(updatedPiece.StartElevation);
        Assert.Null(startElev.Value);
        Assert.NotEmpty(startElev.IssueRefs);

        var ledgers = RouteLedgerCalculator.Compute(state.Pieces);
        var m1Ledger = ledgers.Pieces.Single(p => p.PieceId == "M1");

        Assert.IsType<UnknownValue>(m1Ledger.RouteLength);
        Assert.IsType<UnknownValue>(m1Ledger.CutLength);

        // Totals become unknown because one piece is unknown - unknown is not zero.
        Assert.IsType<UnknownValue>(ledgers.RouteTotal);
        Assert.IsType<UnknownValue>(ledgers.CutTotal);
        Assert.Contains("issue:elevation-unknown:M1:start", ledgers.IssueIds);
    }

    [Fact]
    [Trait("ValidationEvidence", "SyntheticBaseline")]
    [Trait("ValidationScope", "FixtureOnly")]
    public async Task UnknownElevation_DoesNotBecomeZeroInTotals()
    {
        var (service, domainStore, projectId, actor) = CreateServiceWithBaseline();

        var before = RouteLedgerCalculator.Compute((await domainStore.GetAsync(projectId))!.Pieces);
        var beforeRoute = Assert.IsType<KnownValue>(before.RouteTotal);
        // If unknown was treated as zero, total would drop by M1's 3.0m.
        var m1Before = before.Pieces.Single(p => p.PieceId == "M1");
        var m1RouteBefore = Assert.IsType<KnownValue>(m1Before.RouteLength);

        var m1 = (await domainStore.GetAsync(projectId))!.GetRequiredPiece("M1");
        await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-unknown-zero"),
            new UpsertRouteCommand("M1", [new RouteVertex(m1.StartM.X, m1.StartM.Y, m1.StartM.Z), new RouteVertex(m1.EndM.X, m1.EndM.Y, null)], null, null, null),
            actor);

        var after = RouteLedgerCalculator.Compute((await domainStore.GetAsync(projectId))!.Pieces);
        var total = after.RouteTotal;

        Assert.IsNotType<KnownValue>(total);
        var unknown = Assert.IsType<UnknownValue>(total);
        Assert.Equal(SiUnit.Meter, unknown.Unit);
        Assert.Contains("unknown", unknown.Reason, StringComparison.OrdinalIgnoreCase);

        // Ensure it is not silently reduced to known (total - m1).
        Assert.NotEqual(beforeRoute.Value - m1RouteBefore.Value, beforeRoute.Value);
    }

    [Fact]
    public async Task UpsertRoute_MissingRoute_ReturnsIssue_NoMutation()
    {
        var (service, domainStore, projectId, actor) = CreateServiceWithBaseline();
        var beforeRevision = (await domainStore.GetAsync(projectId))!.Revision;

        var receipt = await service.ExecuteCommandAsync(
            projectId,
            ProjectRevision.Initial,
            new CommandId("cmd-missing"),
            new UpsertRouteCommand("DOES_NOT_EXIST", [new RouteVertex(0, 0, 0), new RouteVertex(1, 1, 1)], null, null, null),
            actor);

        Assert.Equal(1, receipt.NewRevision.Value); // command accepted at boundary; handler reports issue
        Assert.Contains(receipt.IssueIds, i => i.Contains("route-not-found", StringComparison.Ordinal));

        var state = await domainStore.GetAsync(projectId);
        Assert.Equal(beforeRevision, state!.Revision); // domain not mutated by handler
    }

    private static (InMemoryProjectCommandService Service, InMemoryProjectDomainStateStore DomainStore, ProjectId ProjectId, ActorContext Actor) CreateServiceWithBaseline()
    {
        var domainStore = new InMemoryProjectDomainStateStore();
        var service = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        var projectId = new ProjectId("project:synthetic-baseline");
        service.CreateProject(projectId);

        var baseline = SyntheticReferenceNetworkLoader.LoadBaselineFromFile(ResolveFixturePath(), projectId);
        domainStore.SaveAsync(baseline).GetAwaiter().GetResult();

        var actor = new ActorContext("actor-1", "session-1");
        return (service, domainStore, projectId, actor);
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