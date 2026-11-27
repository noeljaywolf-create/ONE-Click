namespace OneClick.Application.Agents.Audit;

/// <summary>
/// Execution outcome status for a recorded local agent run.
/// </summary>
public enum AgentRunStatus
{
    Unknown = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
}
