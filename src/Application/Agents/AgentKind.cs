namespace OneClick.Application.Agents;

/// <summary>
/// Classifies local agent behavior. Takeoff agents are local/offline and deterministic where possible.
/// </summary>
public enum AgentKind
{
    Unknown = 0,
    Deterministic = 1,
}