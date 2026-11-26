namespace OneClick.Desktop.ViewModels.Ledger;

public enum LedgerElevationEnd
{
    Start = 0,
    End = 1,
}

/// <summary>
/// Edit request emitted by the ledger panel. The shell/application layer wires this
/// to the appropriate command/service; this control does not mutate domain state directly.
/// </summary>
public sealed record LedgerElevationEditRequest(
    string PieceId,
    LedgerElevationEnd End,
    double? NewElevationMeters,
    bool MakeUnknown);
