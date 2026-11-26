using OneClick.Application.Persistence;

namespace OneClick.Application.Persistence.InMemory;

public sealed class InMemoryCommandTransactionFactory : ICommandTransactionFactory
{
    private readonly InMemoryCommandSynchronization _sync;

    public InMemoryCommandTransactionFactory(InMemoryCommandSynchronization sync)
    {
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
    }

    public Task<ICommandTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Monitor.Enter(_sync.Gate);
        return Task.FromResult<ICommandTransaction>(new InMemoryCommandTransaction(_sync.Gate));
    }

    private sealed class InMemoryCommandTransaction : ICommandTransaction
    {
        private object? _gate;

        public InMemoryCommandTransaction(object gate)
        {
            _gate = gate;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            var gate = Interlocked.Exchange(ref _gate, null);
            if (gate is not null)
            {
                Monitor.Exit(gate);
            }

            return ValueTask.CompletedTask;
        }
    }
}
