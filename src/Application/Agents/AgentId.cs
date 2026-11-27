namespace OneClick.Application.Agents;

/// <summary>
/// Stable identifier for a local agent registration.
/// </summary>
public readonly record struct AgentId(string Value)
{
    public override string ToString() => Value;
}
