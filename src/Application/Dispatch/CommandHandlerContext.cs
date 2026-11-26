using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Core.Ids;

namespace OneClick.Application.Dispatch;

/// <summary>
/// Context passed to command handlers. It exposes validated boundary data; handlers must not
/// treat this as authorization or persistence authority.
/// </summary>
public sealed record CommandHandlerContext(
    ProjectId ProjectId,
    ProjectRevision ExpectedRevision,
    CommandId CommandId,
    ActorContext ActorContext);
