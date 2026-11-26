using OneClick.Application.Contracts;
using OneClick.Core.Ids;

namespace OneClick.Application.Persistence;

/// <summary>
/// Persistence seam for project command state and replay records.
/// Implementations own storage/retrieval; the application service owns command orchestration.
/// This seam does not imply durability, a database engine, or distributed coordination.
/// </summary>
public interface IProjectCommandStore
{
    Task<ProjectCommandState?> GetStateAsync(ProjectId projectId, CancellationToken cancellationToken = default);

    Task CreateProjectAsync(ProjectId projectId, CancellationToken cancellationToken = default);

    Task<StoredReplay?> FindReplayAsync(
        ProjectId projectId,
        string replayKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits an accepted command outcome atomically within the active transaction scope.
    /// Implementations must persist the new revision and replay record together.
    /// </summary>
    Task<CommandReceipt> CommitAcceptedCommandAsync(
        AcceptedCommandCommit commit,
        CancellationToken cancellationToken = default);
}
