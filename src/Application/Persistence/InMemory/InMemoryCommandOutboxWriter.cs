using System.Collections.Concurrent;
using OneClick.Application.Persistence;

namespace OneClick.Application.Persistence.InMemory;

/// <summary>
/// Process-local in-memory outbox writer for tests/skeleton composition. It records messages in memory
/// and provides no durability or delivery.
/// </summary>
public sealed class InMemoryCommandOutboxWriter : ICommandOutboxWriter
{
    private readonly ConcurrentQueue<CommandOutboxMessage> _messages = new();

    public IReadOnlyCollection<CommandOutboxMessage> Messages => _messages.ToArray();

    public Task WriteAsync(CommandOutboxMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(message);
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }
}
