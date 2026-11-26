namespace OneClick.Core.Topology;

/// <summary>
/// Explicit metadata supplied by the caller about a connection candidate. This validator does not
/// detect crossing/proximity; it only enforces invariants on explicitly supplied facts.
/// </summary>
public sealed record ConnectionValidationMetadata
{
    public bool IsCrossingCandidate { get; init; }

    public bool IsProximityOnlyCandidate { get; init; }

    /// <summary>
    /// True when the caller has explicitly identified the endpoint ports for this candidate.
    /// Defaults to true because the request carries explicit Port objects; callers should set false
    /// when the candidate was inferred without explicit endpoint identification.
    /// </summary>
    public bool EndpointsExplicitlyIdentified { get; init; } = true;

    /// <summary>Optional caller-supplied note for traceability (not used for geometric inference).</summary>
    public string? Note { get; init; }
}
