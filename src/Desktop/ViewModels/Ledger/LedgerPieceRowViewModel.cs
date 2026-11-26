using OneClick.Core.Domain;
using OneClick.Core.TaggedValues;

namespace OneClick.Desktop.ViewModels.Ledger;

public sealed class LedgerPieceRowViewModel
{
    public LedgerPieceRowViewModel(RoutePiece piece, RoutePieceLedger? ledger)
    {
        ArgumentNullException.ThrowIfNull(piece);

        PieceId = piece.Id;
        Group = piece.Group;
        StartElevation = LedgerTaggedValueDisplay.FromTaggedValue(piece.StartElevation, "Start elevation");
        EndElevation = LedgerTaggedValueDisplay.FromTaggedValue(piece.EndElevation, "End elevation");
        RouteLength = LedgerTaggedValueDisplay.FromTaggedValue(ledger?.RouteLength, "Route length");
        CutLength = LedgerTaggedValueDisplay.FromTaggedValue(ledger?.CutLength, "Cut length");
        SourceRefs = piece.SourceRefs.Count == 0 ? "(none)" : string.Join(", ", piece.SourceRefs);

        HasUnknown = StartElevation.IsUnknown || EndElevation.IsUnknown || RouteLength.IsUnknown || CutLength.IsUnknown;
        HasProvisional = StartElevation.IsProvisional || EndElevation.IsProvisional || RouteLength.IsProvisional || CutLength.IsProvisional;

        var issues = new List<string>();
        CollectIssues(issues, piece.StartElevation);
        CollectIssues(issues, piece.EndElevation);
        CollectIssues(issues, ledger?.RouteLength);
        CollectIssues(issues, ledger?.CutLength);
        Issues = issues.Distinct(StringComparer.Ordinal).ToArray();
        IssueSummary = Issues.Count == 0 ? string.Empty : string.Join(", ", Issues);
    }

    public string PieceId { get; }
    public string Group { get; }
    public LedgerTaggedValueDisplay StartElevation { get; }
    public LedgerTaggedValueDisplay EndElevation { get; }
    public LedgerTaggedValueDisplay RouteLength { get; }
    public LedgerTaggedValueDisplay CutLength { get; }
    public string SourceRefs { get; }
    public bool HasUnknown { get; }
    public bool HasProvisional { get; }
    public IReadOnlyList<string> Issues { get; }
    public string IssueSummary { get; }

    private static void CollectIssues(List<string> issues, TaggedValue? value)
    {
        if (value is UnknownValue unknown)
        {
            foreach (var issue in unknown.IssueRefs)
                issues.Add(issue);
        }
        else if (value is ProvisionalValue provisional)
        {
            foreach (var issue in provisional.IssueRefs)
                issues.Add(issue);
        }
    }
}
