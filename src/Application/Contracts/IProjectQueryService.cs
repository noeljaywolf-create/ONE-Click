using OneClick.Core.Ids;

namespace OneClick.Application.Contracts;

/// <summary>
/// Application query boundary for accepted project state.
/// </summary>
public interface IProjectQueryService
{
    Task<ProjectView> QueryProjectAsync(
        ProjectId projectId,
        ActorContext actorContext,
        CancellationToken cancellationToken = default);
}
