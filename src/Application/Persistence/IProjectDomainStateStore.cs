using OneClick.Core.Domain;
using OneClick.Core.Ids;

namespace OneClick.Application.Persistence;

/// <summary>
/// Hook for command handlers and snapshot/export to access in-memory project domain state.
/// This is a minimal first-slice seam; it does not imply DB persistence or transactional semantics.
/// </summary>
public interface IProjectDomainStateStore
{
    Task<ProjectDomainState?> GetAsync(ProjectId projectId, CancellationToken cancellationToken = default);

    Task<ProjectDomainSnapshot?> GetSnapshotAsync(ProjectId projectId, CancellationToken cancellationToken = default);

    Task SaveAsync(ProjectDomainState state, CancellationToken cancellationToken = default);
}
