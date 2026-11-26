namespace OneClick.Application.Persistence;

/// <summary>
/// Transaction seam for command execution boundaries.
/// Implementations may map this to a database transaction, a process-local lock, or another atomic scope.
/// This interface does not claim durability by itself.
/// </summary>
public interface ICommandTransactionFactory
{
    Task<ICommandTransaction> BeginAsync(CancellationToken cancellationToken = default);
}
