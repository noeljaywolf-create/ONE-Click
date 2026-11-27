namespace OneClick.Application.Agents.Audit;

/// <summary>
/// Stable identifier for a single local agent run audit record.
/// </summary>
public readonly record struct AgentRunId(string Value)
{
    public static AgentRunId NewId() => new($"agent-run:{Guid.NewGuid():N}");

    public override string ToString() => Value;
}
