namespace OneClick.Application.Persistence.InMemory;

/// <summary>
/// Process-local synchronization primitive shared by in-memory command store/transaction seams.
/// Not durable; not distributed.
/// </summary>
public sealed class InMemoryCommandSynchronization
{
    public object Gate { get; } = new();
}
