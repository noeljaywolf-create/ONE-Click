using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Core.Ids;

namespace OneClick.Application.Persistence;

/// <summary>
/// Atomic commit payload for an accepted command. The store persists revision/replay state;
/// the outbox seam receives the emitted message separately.
/// </summary>
public sealed record AcceptedCommandCommit(
    ProjectId ProjectId,
    ProjectRevision ExpectedRevision,
    ProjectRevision NewRevision,
    CommandId CommandId,
    string ReplayKey,
    string BodyHash,
    ProjectCommand Command,
    ActorContext ActorContext,
    CommandReceipt Receipt);
