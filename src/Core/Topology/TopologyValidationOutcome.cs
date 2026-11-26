namespace OneClick.Core.Topology;

/// <summary>
/// Explicit topology/connection validation outcome. These outcomes model candidate resolution
/// without implementing a geometry engine or topology solver.
/// </summary>
public enum TopologyValidationOutcome
{
    Resolved = 1,
    Alternatives = 2,
    Infeasible = 3,
    Unsupported = 4,
    Incomplete = 5,
}
