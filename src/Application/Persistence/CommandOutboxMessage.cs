using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Core.Ids;

namespace OneClick.Application.Persistence;

/// <summary>
/// Outbox message seam emitted after an accepted command commit. It is a contract placeholder;
/// durable storage/delivery is a later infrastructure concern.
/// </summary>
public sealed record CommandOutboxMessage(
    string MessageId,
    ProjectId ProjectId,
    ProjectRevision NewRevision,
    CommandId CommandId,
    string CommandType,
    DateTimeOffset OccurredAtUtc,
    CommandReceipt Receipt);
