namespace OneClick.Core.Provenance;

/// <summary>
/// Contract for derived results. Every derived result must expose provenance with input versions,
/// method version, and traceable contributions.
/// </summary>
public interface IDerivedResult
{
    ResultProvenance Provenance { get; }
}
