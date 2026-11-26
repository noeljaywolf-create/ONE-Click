namespace OneClick.Core.Topology;

/// <summary>
/// Stable issue codes for topology connection validation. These are application-defined identifiers,
/// not external standards codes.
/// </summary>
public static class TopologyIssueCodes
{
    public const string PortNotIdentified = "topology.port_not_identified";
    public const string SamePort = "topology.same_port";
    public const string CrossingNotConnection = "topology.crossing_not_connection";
    public const string ProximityNotConnection = "topology.proximity_not_connection";
    public const string EndpointNotExplicitlyIdentified = "topology.endpoint_not_explicitly_identified";
    public const string IncompatibleInterface = "topology.incompatible_interface";
    public const string IncompatiblePreparation = "topology.incompatible_preparation";
    public const string CompatibilityUnknown = "topology.compatibility_unknown";
}
