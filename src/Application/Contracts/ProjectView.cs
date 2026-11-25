namespace OneClick.Application.Contracts;

/// <summary>
/// Read model summary returned by QueryProject. This is a contract placeholder; fields will expand as gates are implemented.
/// </summary>
public sealed record ProjectView(
    string ProjectId,
    ProjectRevision Revision,
    ProjectCapabilities Capabilities,
    ProjectScope Scope);

public sealed record ProjectCapabilities(
    IReadOnlyList<string> SupportedCommands,
    IReadOnlyList<string> SupportedQueries);

public sealed record ProjectScope(
    bool HasAcceptedModel,
    IReadOnlyList<string> KnownLimitations);
