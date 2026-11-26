using OneClick.Application.Contracts;

namespace OneClick.Application.Dispatch;

/// <summary>
/// Structured result returned by a command handler. Handlers report affected IDs and issues;
/// they do not perform persistence, transaction commit, or transport mapping.
/// </summary>
public sealed record CommandHandlerResult(
    IReadOnlyList<string> CreatedIds,
    IReadOnlyList<string> InvalidatedIds,
    IReadOnlyList<string> IssueIds)
{
    public static CommandHandlerResult Empty { get; } =
        new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
}
