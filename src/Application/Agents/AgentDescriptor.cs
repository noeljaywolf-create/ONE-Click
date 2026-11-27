namespace OneClick.Application.Agents;

/// <summary>
/// Describes a registered local agent. Descriptors are immutable metadata; execution behavior
/// lives in the agent implementation.
/// </summary>
public sealed record AgentDescriptor(
    AgentId Id,
    AgentKind Kind,
    string Name,
    string Description,
    string Version)
{
    public AgentId Id { get; } = ValidateId(Id);
    public string Name { get; } = Require(Name, nameof(Name));
    public string Description { get; } = Require(Description, nameof(Description));
    public string Version { get; } = Require(Version, nameof(Version));

    private static AgentId ValidateId(AgentId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
            throw new ArgumentException("Agent id is required.", nameof(id));

        return id;
    }

    private static string Require(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", paramName);

        return value;
    }
}
