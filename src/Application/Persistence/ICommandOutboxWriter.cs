using OneClick.Application.Contracts;
using OneClick.Core.Ids;

namespace OneClick.Application.Persistence;

/// <summary>
/// Outbox seam for command side effects/events. Implementations may persist, queue, or discard
/// messages in tests; no delivery guarantee is implied by this skeleton.
/// </summary>
public interface ICommandOutboxWriter
{
    Task WriteAsync(CommandOutboxMessage message, CancellationToken cancellationToken = default);
}
