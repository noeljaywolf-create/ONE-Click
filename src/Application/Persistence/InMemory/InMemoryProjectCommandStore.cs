using OneClick.Application.Contracts;
using OneClick.Application.Errors;
using OneClick.Application.Persistence;
using OneClick.Core.Ids;

namespace OneClick.Application.Persistence.InMemory;

/// <summary>
/// Process-local in-memory command store. State is not durable and is lost on process exit.
/// </summary>
public sealed class InMemoryProjectCommandStore : IProjectCommandStore
{
    private readonly InMemoryCommandSynchronization _sync;
    private readonly Dictionary<string, ProjectState> _projects = new(StringComparer.Ordinal);

    public InMemoryProjectCommandStore(InMemoryCommandSynchronization sync)
    {
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
    }

    public Task<ProjectCommandState?> GetStateAsync(ProjectId projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(projectId.Value);

        lock (_sync.Gate)
        {
            return Task.FromResult(_projects.TryGetValue(projectId.Value, out var state)
                ? new ProjectCommandState(new ProjectId(state.ProjectId), state.Revision)
                : null);
        }
    }

    public Task CreateProjectAsync(ProjectId projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(projectId.Value))
        {
            throw new ApplicationProblemException(Malformed("ProjectId is required.", "projectId"));
        }

        lock (_sync.Gate)
        {
            if (_projects.ContainsKey(projectId.Value))
            {
                throw new ApplicationProblemException(Conflict(
                    $"Project '{projectId.Value}' already exists.",
                    "projectId",
                    null));
            }

            _projects[projectId.Value] = new ProjectState(projectId.Value);
            return Task.CompletedTask;
        }
    }

    public Task<StoredReplay?> FindReplayAsync(
        ProjectId projectId,
        string replayKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(replayKey);

        lock (_sync.Gate)
        {
            if (!_projects.TryGetValue(projectId.Value, out var state))
            {
                return Task.FromResult<StoredReplay?>(null);
            }

            return Task.FromResult(state.ReplayEntries.TryGetValue(replayKey, out var replay)
                ? new StoredReplay(replayKey, replay.BodyHash, replay.Receipt)
                : null);
        }
    }

    public Task<CommandReceipt> CommitAcceptedCommandAsync(
        AcceptedCommandCommit commit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(commit);

        lock (_sync.Gate)
        {
            if (!_projects.TryGetValue(commit.ProjectId.Value, out var state))
            {
                throw new ApplicationProblemException(NotFound(
                    $"Project '{commit.ProjectId.Value}' was not found.",
                    "projectId"));
            }

            if (state.Revision != commit.ExpectedRevision)
            {
                throw new ApplicationProblemException(StaleRevision(state.Revision));
            }

            if (state.ReplayEntries.ContainsKey(commit.ReplayKey))
            {
                throw new ApplicationProblemException(Conflict(
                    "Command replay key was already committed.",
                    "commandId",
                    state.Revision));
            }

            state.Revision = commit.NewRevision;
            state.ReplayEntries[commit.ReplayKey] = new ReplayEntry(commit.BodyHash, commit.Receipt);
            return Task.FromResult(commit.Receipt);
        }
    }

    private static ApplicationProblemDetails Malformed(string detail, string field) =>
        new(
            ApplicationProblemTypes.MalformedPayload,
            "Malformed command payload",
            400,
            detail,
            $"urn:oneclick:validation:{field}",
            ApplicationProblemCodes.MalformedPayload,
            Array.Empty<string>(),
            null);

    private static ApplicationProblemDetails NotFound(string detail, string field) =>
        new(
            ApplicationProblemTypes.NotFound,
            "Resource not found",
            404,
            detail,
            $"urn:oneclick:validation:{field}",
            ApplicationProblemCodes.NotFound,
            Array.Empty<string>(),
            null);

    private static ApplicationProblemDetails Conflict(string detail, string field, ProjectRevision? currentRevision) =>
        new(
            ApplicationProblemTypes.Conflict,
            "Command conflict",
            409,
            detail,
            $"urn:oneclick:validation:{field}",
            ApplicationProblemCodes.Conflict,
            Array.Empty<string>(),
            currentRevision);

    private static ApplicationProblemDetails StaleRevision(ProjectRevision currentRevision) =>
        new(
            ApplicationProblemTypes.StaleRevision,
            "Stale project revision",
            412,
            "Expected revision does not match current project revision.",
            "urn:oneclick:validation:expectedRevision",
            ApplicationProblemCodes.StaleRevision,
            Array.Empty<string>(),
            currentRevision);

    private sealed class ProjectState(string projectId)
    {
        public string ProjectId { get; } = projectId;
        public ProjectRevision Revision { get; set; } = ProjectRevision.Initial;
        public Dictionary<string, ReplayEntry> ReplayEntries { get; } = new(StringComparer.Ordinal);
    }

    private sealed record ReplayEntry(string BodyHash, CommandReceipt Receipt);
}
