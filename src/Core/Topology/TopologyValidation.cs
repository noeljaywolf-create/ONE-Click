namespace OneClick.Core.Topology;

/// <summary>
/// Static facade for topology validation entry points. Keeps call sites aligned with the
/// documented internal signature while delegating to deterministic Core validation.
/// </summary>
public static class TopologyValidation
{
    public static CandidateResult ValidateConnection(
        Port? portA,
        Port? portB,
        ConnectionValidationMetadata? metadata = null,
        IPortInterfaceCompatibilityPolicy? compatibilityPolicy = null)
    {
        return new TopologyConnectionValidator().ValidateConnection(portA, portB, metadata, compatibilityPolicy);
    }
}
