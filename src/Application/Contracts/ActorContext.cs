namespace OneClick.Application.Contracts;

/// <summary>
/// Actor identity resolved from the authenticated session.
/// This is not caller-supplied authority; authorization decisions are made by the application boundary.
/// </summary>
public sealed record ActorContext(
    string ActorId,
    string SessionId,
    string? DisplayName = null);
