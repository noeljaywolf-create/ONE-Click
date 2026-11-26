namespace OneClick.Application.Persistence;

/// <summary>
/// Represents an active command execution transaction/scope.
/// </summary>
public interface ICommandTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
