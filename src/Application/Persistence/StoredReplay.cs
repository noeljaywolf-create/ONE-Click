using OneClick.Application.Contracts;

namespace OneClick.Application.Persistence;

/// <summary>
/// Stored replay record for an operation-id scoped command.
/// </summary>
public sealed record StoredReplay(
    string ReplayKey,
    string BodyHash,
    CommandReceipt Receipt);
