using OneClick.Core.Ids;

namespace OneClick.Core.Topology;

/// <summary>
/// Explicit inputs for connection validation. No geometric crossing detection or auto-connection
/// is performed; callers supply ports and optional candidate metadata.
/// </summary>
public sealed record ConnectionValidationRequest
{
    public ConnectionValidationRequest(
        Port? portA,
        Port? portB,
        ConnectionValidationMetadata? metadata = null,
        IPortInterfaceCompatibilityPolicy? compatibilityPolicy = null)
    {
        PortA = portA;
        PortB = portB;
        Metadata = metadata ?? new ConnectionValidationMetadata();
        CompatibilityPolicy = compatibilityPolicy;
    }

    public Port? PortA { get; }

    public Port? PortB { get; }

    public ConnectionValidationMetadata Metadata { get; }

    /// <summary>
    /// Optional catalogue/interface compatibility hook. When null, the validator uses
    /// <see cref="StrictPortInterfaceCompatibilityPolicy"/> only for explicit interface/preparation checks.
    /// </summary>
    public IPortInterfaceCompatibilityPolicy? CompatibilityPolicy { get; }
}
