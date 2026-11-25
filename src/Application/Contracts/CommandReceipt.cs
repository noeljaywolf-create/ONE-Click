namespace OneClick.Application.Contracts;

/// <summary>
/// Receipt returned by an accepted command. It reports the committed revision and the IDs affected by the command.
/// </summary>
public sealed record CommandReceipt(
    ProjectRevision NewRevision,
    IReadOnlyList<string> CreatedIds,
    IReadOnlyList<string> InvalidatedIds,
    IReadOnlyList<string> IssueIds);
