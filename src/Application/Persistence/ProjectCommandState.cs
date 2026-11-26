using OneClick.Application.Contracts;
using OneClick.Core.Ids;

namespace OneClick.Application.Persistence;

/// <summary>
/// Minimal persisted command-state view required by the command execution boundary.
/// </summary>
public sealed record ProjectCommandState(
    ProjectId ProjectId,
    ProjectRevision Revision);
