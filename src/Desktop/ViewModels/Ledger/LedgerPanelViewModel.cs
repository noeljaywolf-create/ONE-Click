using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using OneClick.Core.Domain;

namespace OneClick.Desktop.ViewModels.Ledger;

public sealed class LedgerPanelViewModel : ViewModelBase
{
    private LedgerPieceRowViewModel? _selectedPiece;
    private LedgerElevationEnd _selectedElevationEnd = LedgerElevationEnd.Start;
    private string _elevationValueText = string.Empty;
    private string _statusMessage = "Load a project state to view route and cut ledgers.";
    private bool _isStatusError;
    private LedgerTaggedValueDisplay _routeTotal = LedgerTaggedValueDisplay.FromTaggedValue(null, "Route total");
    private LedgerTaggedValueDisplay _cutTotal = LedgerTaggedValueDisplay.FromTaggedValue(null, "Cut total");
    private string _issuesSummary = "No issues.";
    private bool _hasIssues;
    private int _issueCount;
    private string _routeBreakdownSummary = "Known: 0, Provisional: 0, Unknown: 0";
    private string _cutBreakdownSummary = "Known: 0, Provisional: 0, Unknown: 0";
    private string _elevationInputError = string.Empty;
    private bool _isBusy;
    private ICommand _changeElevationCommand;
    private ICommand _makeElevationUnknownCommand;

    public LedgerPanelViewModel()
    {
        _changeElevationCommand = new RelayCommand(async () => await ChangeElevationAsync(), () => CanSetKnownElevation);
        _makeElevationUnknownCommand = new RelayCommand(async () => await MakeElevationUnknownAsync(), () => CanMakeUnknownElevation);
    }

    public ObservableCollection<LedgerPieceRowViewModel> Pieces { get; } = new();

    public LedgerPieceRowViewModel? SelectedPiece
    {
        get => _selectedPiece;
        set
        {
            if (!SetProperty(ref _selectedPiece, value))
                return;

            SyncElevationInputFromSelection();
            RaiseSelectionProperties();
            RaiseCanExecuteChanged();
        }
    }

    public LedgerElevationEnd SelectedElevationEnd
    {
        get => _selectedElevationEnd;
        set
        {
            if (!SetProperty(ref _selectedElevationEnd, value))
                return;

            SyncElevationInputFromSelection();
            RaiseSelectionProperties();
        }
    }

    public IReadOnlyList<LedgerElevationEnd> ElevationEnds { get; } =
    [
        LedgerElevationEnd.Start,
        LedgerElevationEnd.End,
    ];

    public string ElevationValueText
    {
        get => _elevationValueText;
        set
        {
            if (!SetProperty(ref _elevationValueText, value))
                return;

            ValidateElevationInput();
            RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    public LedgerTaggedValueDisplay RouteTotal
    {
        get => _routeTotal;
        private set => SetProperty(ref _routeTotal, value);
    }

    public LedgerTaggedValueDisplay CutTotal
    {
        get => _cutTotal;
        private set => SetProperty(ref _cutTotal, value);
    }

    public string IssuesSummary
    {
        get => _issuesSummary;
        private set => SetProperty(ref _issuesSummary, value);
    }

    public bool HasIssues
    {
        get => _hasIssues;
        private set => SetProperty(ref _hasIssues, value);
    }

    public int IssueCount
    {
        get => _issueCount;
        private set => SetProperty(ref _issueCount, value);
    }

    public string RouteBreakdownSummary
    {
        get => _routeBreakdownSummary;
        private set => SetProperty(ref _routeBreakdownSummary, value);
    }

    public string CutBreakdownSummary
    {
        get => _cutBreakdownSummary;
        private set => SetProperty(ref _cutBreakdownSummary, value);
    }

    public string ElevationInputError
    {
        get => _elevationInputError;
        private set
        {
            if (!SetProperty(ref _elevationInputError, value))
                return;
            RaisePropertyChanged(nameof(HasElevationInputError));
            RaisePropertyChanged(nameof(IsElevationInputValid));
            RaisePropertyChanged(nameof(CanSetKnownElevation));
        }
    }

    public bool HasElevationInputError => !string.IsNullOrWhiteSpace(ElevationInputError);
    public bool IsElevationInputValid => !HasElevationInputError;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
                return;
            RaiseSelectionProperties();
            RaiseCanExecuteChanged();
        }
    }

    public bool CanEditSelectedPiece => SelectedPiece is not null && !IsBusy;
    public bool CanSetKnownElevation => CanEditSelectedPiece && IsElevationInputValid && !string.IsNullOrWhiteSpace(ElevationValueText);
    public bool CanMakeUnknownElevation => CanEditSelectedPiece;

    public string SelectedPieceLabel => SelectedPiece is null
        ? "No piece selected. Select a piece row to edit elevations."
        : $"Piece {SelectedPiece.PieceId} ({SelectedPiece.Group})";

    public string SelectedEndLabel => SelectedElevationEnd == LedgerElevationEnd.Start ? "Start end" : "End end";

    public LedgerTaggedValueDisplay? SelectedEndCurrentValue
    {
        get
        {
            if (SelectedPiece is null) return null;
            return SelectedElevationEnd == LedgerElevationEnd.Start ? SelectedPiece.StartElevation : SelectedPiece.EndElevation;
        }
    }

    public string SelectedEndCurrentValueText => SelectedEndCurrentValue?.DisplayText ?? "No selection";
    public string SelectedEndCurrentStatusText => SelectedEndCurrentValue?.Status ?? "-";
    public string SelectedEndCurrentDetailText => SelectedEndCurrentValue?.DetailText ?? "Select a piece and end to view its current elevation state.";

    /// <summary>
    /// Bindable command property for changing the selected piece elevation to a known meter value.
    /// The shell can replace this command or wire <see cref="OnElevationEditRequested"/>.
    /// </summary>
    public ICommand ChangeElevationCommand
    {
        get => _changeElevationCommand;
        set
        {
            if (SetProperty(ref _changeElevationCommand, value))
                RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Bindable command property for making the selected piece elevation unknown.
    /// The shell can replace this command or wire <see cref="OnElevationEditRequested"/>.
    /// </summary>
    public ICommand MakeElevationUnknownCommand
    {
        get => _makeElevationUnknownCommand;
        set
        {
            if (SetProperty(ref _makeElevationUnknownCommand, value))
                RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Application/shell callback. The control invokes this when the user requests an elevation edit.
    /// The shell should connect this to Application services/commands.
    /// Return a status message to display, or null to use the default completion message.
    /// </summary>
    public Func<LedgerElevationEditRequest, Task<string?>>? OnElevationEditRequested { get; set; }

    public void Refresh(IEnumerable<RoutePiece> pieces, ProjectRouteLedgers? ledgers = null)
    {
        ArgumentNullException.ThrowIfNull(pieces);

        var pieceList = pieces as IReadOnlyList<RoutePiece> ?? pieces.ToArray();
        var computedLedgers = ledgers ?? RouteLedgerCalculator.Compute(pieceList);
        var ledgerById = computedLedgers.Pieces.ToDictionary(p => p.PieceId, StringComparer.Ordinal);

        var selectedId = SelectedPiece?.PieceId;
        Pieces.Clear();
        foreach (var piece in pieceList.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            ledgerById.TryGetValue(piece.Id, out var ledger);
            Pieces.Add(new LedgerPieceRowViewModel(piece, ledger));
        }

        RouteTotal = LedgerTaggedValueDisplay.FromTaggedValue(computedLedgers.RouteTotal, "Route total");
        CutTotal = LedgerTaggedValueDisplay.FromTaggedValue(computedLedgers.CutTotal, "Cut total");
        UpdateBreakdowns(computedLedgers);

        var (issueSummary, hasIssues, issueCount) = BuildIssuesSummary(computedLedgers);
        IssuesSummary = issueSummary;
        HasIssues = hasIssues;
        IssueCount = issueCount;

        if (selectedId is not null)
            SelectedPiece = Pieces.FirstOrDefault(p => string.Equals(p.PieceId, selectedId, StringComparison.Ordinal));
        else
            RaiseSelectionProperties();

        SetStatus($"Loaded {Pieces.Count} piece(s). Route and cut totals are separate ledgers. Unknown is never displayed as zero.", isError: false);
        RaiseCanExecuteChanged();
    }

    public void Refresh(ProjectDomainState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Refresh(state.Pieces);
    }

    private async Task ChangeElevationAsync()
    {
        if (SelectedPiece is null)
        {
            SetStatus("Select a piece before changing elevation.", isError: true);
            return;
        }

        if (!TryParseElevation(ElevationValueText, out var elevation))
        {
            ValidateElevationInput(forceRequired: true);
            SetStatus(ElevationInputError.Length > 0 ? ElevationInputError : "Enter a finite elevation value in meters.", isError: true);
            return;
        }

        var request = new LedgerElevationEditRequest(
            SelectedPiece.PieceId,
            SelectedElevationEnd,
            elevation,
            MakeUnknown: false);

        await DispatchEditAsync(request, $"Requested elevation change for {request.PieceId} {request.End} to {elevation.ToString("0.###", CultureInfo.InvariantCulture)} m.");
    }

    private async Task MakeElevationUnknownAsync()
    {
        if (SelectedPiece is null)
        {
            SetStatus("Select a piece before making elevation unknown.", isError: true);
            return;
        }

        var request = new LedgerElevationEditRequest(
            SelectedPiece.PieceId,
            SelectedElevationEnd,
            NewElevationMeters: null,
            MakeUnknown: true);

        await DispatchEditAsync(request, $"Requested making {request.PieceId} {request.End} elevation unknown.");
    }

    private async Task DispatchEditAsync(LedgerElevationEditRequest request, string pendingMessage)
    {
        if (OnElevationEditRequested is null)
        {
            SetStatus(pendingMessage + " No application handler is wired; shell must set OnElevationEditRequested or replace command properties.", isError: true);
            return;
        }

        try
        {
            IsBusy = true;
            SetStatus(pendingMessage, isError: false);
            var completionStatus = await OnElevationEditRequested(request).ConfigureAwait(true);
            SetStatus(string.IsNullOrWhiteSpace(completionStatus) ? pendingMessage + " Completed." : completionStatus, isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Elevation edit failed: {ex.Message}", isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SyncElevationInputFromSelection()
    {
        if (SelectedPiece is not null)
        {
            var display = SelectedElevationEnd == LedgerElevationEnd.Start ? SelectedPiece.StartElevation : SelectedPiece.EndElevation;
            ElevationValueText = display.NumericValue?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;
        }

        ValidateElevationInput();
    }

    private void ValidateElevationInput(bool forceRequired = false)
    {
        var text = ElevationValueText?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            ElevationInputError = forceRequired ? "Elevation is required to set a known value. Use 'Make elevation unknown' to clear it." : string.Empty;
            return;
        }

        if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            && !double.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out value))
        {
            ElevationInputError = "Enter a finite numeric elevation in meters (example: 12.5).";
            return;
        }

        if (!double.IsFinite(value))
        {
            ElevationInputError = "Elevation must be a finite number in meters (NaN/Infinity are not allowed).";
            return;
        }

        ElevationInputError = string.Empty;
    }

    private static bool TryParseElevation(string? text, out double value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.Trim();
        if (!double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)
            && !double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out value))
        {
            return false;
        }

        return double.IsFinite(value);
    }

    private void UpdateBreakdowns(ProjectRouteLedgers ledgers)
    {
        var routeKnown = 0;
        var routeProvisional = 0;
        var routeUnknown = 0;
        var cutKnown = 0;
        var cutProvisional = 0;
        var cutUnknown = 0;

        foreach (var piece in ledgers.Pieces)
        {
            CountStatus(piece.RouteLength, ref routeKnown, ref routeProvisional, ref routeUnknown);
            CountStatus(piece.CutLength, ref cutKnown, ref cutProvisional, ref cutUnknown);
        }

        RouteBreakdownSummary = FormatBreakdown(routeKnown, routeProvisional, routeUnknown);
        CutBreakdownSummary = FormatBreakdown(cutKnown, cutProvisional, cutUnknown);
    }

    private static void CountStatus(OneClick.Core.TaggedValues.TaggedValue? value, ref int known, ref int provisional, ref int unknown)
    {
        switch (value)
        {
            case OneClick.Core.TaggedValues.KnownValue:
                known++;
                break;
            case OneClick.Core.TaggedValues.ProvisionalValue:
                provisional++;
                break;
            case OneClick.Core.TaggedValues.UnknownValue:
            case null:
                unknown++;
                break;
            default:
                unknown++;
                break;
        }
    }

    private static string FormatBreakdown(int known, int provisional, int unknown)
        => $"Known: {known}, Provisional: {provisional}, Unknown: {unknown}";

    private static (string Summary, bool HasIssues, int Count) BuildIssuesSummary(ProjectRouteLedgers ledgers)
    {
        var issues = ledgers.IssueIds
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(i => i, StringComparer.Ordinal)
            .ToArray();

        if (issues.Length == 0)
            return ("No active issues. Unknowns, where present, are explicitly marked and are not treated as zero.", false, 0);

        return ($"Issues ({issues.Length}): {string.Join(", ", issues)}", true, issues.Length);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }

    private void RaiseSelectionProperties()
    {
        RaisePropertyChanged(nameof(CanEditSelectedPiece));
        RaisePropertyChanged(nameof(CanSetKnownElevation));
        RaisePropertyChanged(nameof(CanMakeUnknownElevation));
        RaisePropertyChanged(nameof(SelectedPieceLabel));
        RaisePropertyChanged(nameof(SelectedEndLabel));
        RaisePropertyChanged(nameof(SelectedEndCurrentValue));
        RaisePropertyChanged(nameof(SelectedEndCurrentValueText));
        RaisePropertyChanged(nameof(SelectedEndCurrentStatusText));
        RaisePropertyChanged(nameof(SelectedEndCurrentDetailText));
    }

    private void RaiseCanExecuteChanged()
    {
        if (_changeElevationCommand is RelayCommand change)
            change.RaiseCanExecuteChanged();
        if (_makeElevationUnknownCommand is RelayCommand makeUnknown)
            makeUnknown.RaiseCanExecuteChanged();
    }
}
