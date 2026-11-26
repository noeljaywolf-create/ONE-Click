namespace OneClick.Core.Topology;

/// <summary>
/// Lifecycle status for a connection candidate. A nearby endpoint or crossing is never a confirmed
/// connection until explicitly validated and confirmed.
/// </summary>
public enum ConnectionCandidateStatus
{
    Candidate = 1,
    Confirmed = 2,
    Rejected = 3,
}
