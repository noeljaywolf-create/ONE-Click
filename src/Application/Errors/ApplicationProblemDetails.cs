using OneClick.Application.Contracts;

namespace OneClick.Application.Errors;

/// <summary>
/// RFC 9457-aligned problem-details placeholder for application boundaries.
/// Transport adapters may serialize this as application/problem+json.
/// </summary>
public sealed record ApplicationProblemDetails(
    string Type,
    string Title,
    int Status,
    string? Detail,
    string? Instance,
    string Code,
    IReadOnlyList<string> IssueIds,
    ProjectRevision? CurrentRevision);
