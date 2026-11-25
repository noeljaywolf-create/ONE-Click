using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Core.Ids;

namespace OneClick.Application.Validation;

/// <summary>
/// Boundary inputs for Gate 0 command validation. This mirrors the command service boundary
/// so validation can be performed before persistence or execution-engine work.
/// </summary>
public sealed record CommandValidationRequest(
    ProjectId ProjectId,
    ProjectRevision ExpectedRevision,
    CommandId CommandId,
    ProjectCommand? Command,
    ActorContext? ActorContext);
