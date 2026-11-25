namespace OneClick.Core.Provenance;

/// <summary>
/// Distinct quantity ledgers. Numeric totals with different conventions may not be summed
/// even if both use metres.
/// </summary>
public enum Ledger
{
    Route = 1,
    Fabrication = 2,
    Hydraulic = 3,
    Procurement = 4,
}
