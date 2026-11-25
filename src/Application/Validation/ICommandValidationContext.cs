using OneClick.Application.Contracts;
using OneClick.Core.Ids;

namespace OneClick.Application.Validation;

/// <summary>
/// Optional hooks for command-validation checks that require project-scoped persistence or catalog context.
/// Gate 0 provides this interface only; implementations are supplied by later infrastructure/application services.
/// </summary>
public interface ICommandValidationContext
{
    /// <summary>
    /// Returns true when the project exists and is in scope for the current actor/session boundary.
    /// </summary>
    Task<bool> ProjectExistsAsync(ProjectId projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when a referenced entity exists within the project scope.
    /// The reference kind is a stable boundary label such as "region", "port", or "snapshot".
    /// </summary>
    Task<bool> ReferenceExistsInProjectAsync(
        ProjectId projectId,
        string referenceKind,
        string referenceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current committed project revision when known. Used for expected-revision precondition checks.
    /// </summary>
    Task<ProjectRevision?> GetCurrentRevisionAsync(ProjectId projectId, CancellationToken cancellationToken = default);
}
