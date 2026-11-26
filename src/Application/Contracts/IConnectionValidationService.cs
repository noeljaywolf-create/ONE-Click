using OneClick.Core.Topology;

namespace OneClick.Application.Contracts;

/// <summary>
/// Application wiring shape for deterministic connection validation. Implementations delegate to
/// Core validation; this contract does not imply persistence or database behavior.
/// </summary>
public interface IConnectionValidationService
{
    ConnectionValidationResponse ValidateConnection(ConnectionValidationRequestDto request);
}

public sealed record ConnectionValidationRequestDto(
    Port PortA,
    Port PortB,
    ConnectionValidationMetadata? Metadata = null);

public sealed record ConnectionValidationResponse(
    CandidateResult Result,
    bool IsConfirmedConnectionAllowed);
