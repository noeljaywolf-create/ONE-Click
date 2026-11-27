using OneClick.Application.Contracts;
using OneClick.Application.Persistence;
using OneClick.Application.Persistence.InMemory;
using OneClick.Application.Services;
using OneClick.Core.Domain;
using OneClick.Core.Ids;

namespace OneClick.Desktop.Services;

public sealed record DesktopBootstrapResult(
    ProjectId ProjectId,
    ProjectRevision Revision,
    int PieceCount,
    int SnapshotManifestCount,
    string StatusMessage,
    IProjectCommandService CommandService,
    IProjectQueryService QueryService,
    IProjectDomainStateStore DomainStateStore,
    ActorContext ActorContext,
    DesktopAgentRuntimeService AgentRuntime);

public sealed class DesktopProjectBootstrapper
{
    public const string DefaultProjectId = "project:synthetic-baseline";
    public const string DefaultActorId = "desktop-user";
    public const string DefaultSessionId = "desktop-session";

    public async Task<DesktopBootstrapResult> LoadSyntheticBaselineAsync(CancellationToken cancellationToken = default)
    {
        var fixturePath = BaselineFixtureLocator.ResolveFixturePath();
        var projectId = new ProjectId(DefaultProjectId);
        var actor = new ActorContext(DefaultActorId, DefaultSessionId);

        var domainStore = new InMemoryProjectDomainStateStore();
        var commandService = new InMemoryProjectCommandService(validator: null, domainStateStore: domainStore);
        var agentRuntime = new DesktopAgentRuntimeService(domainStore);

        commandService.CreateProject(projectId);

        var baseline = SyntheticReferenceNetworkLoader.LoadBaselineFromFile(fixturePath, projectId);
        await domainStore.SaveAsync(baseline, cancellationToken).ConfigureAwait(true);

        var saved = await domainStore.GetSnapshotAsync(projectId, cancellationToken).ConfigureAwait(true);
        if (saved is null)
            throw new InvalidOperationException($"Synthetic baseline was not present in domain state store after load for project '{projectId.Value}'.");

        var view = await commandService.QueryProjectAsync(projectId, actor, cancellationToken).ConfigureAwait(true);

        var status = $"Synthetic baseline loaded. {saved.Pieces.Count} route piece(s), {saved.SnapshotManifests.Count} snapshot manifest(s).";

        return new DesktopBootstrapResult(
            projectId,
            view.Revision,
            saved.Pieces.Count,
            saved.SnapshotManifests.Count,
            status,
            commandService,
            commandService,
            domainStore,
            actor,
            agentRuntime);
    }
}
