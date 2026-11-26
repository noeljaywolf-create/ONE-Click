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
    private LedgerTaggedValueDisplay _routeTotal = LedgerTaggedValueDisplay.FromTaggedValue(null, "Route total");
    private LedgerTaggedValueDisplay _cutTotal = LedgerTaggedValueDisplay.FromTaggedValue(null, "Cut total");
    private string _issuesSummary = "No issues.";
    private bool _isBusy;
    private ICommand _changeElevationCommand;
    private ICommand _makeElevationUnknownCommand;

    public LedgerPanelViewModel()
    {
        _changeElevationCommand = new RelayCommand(async () => await ChangeElevationAsync(), () => CanEditSelectedPiece);
        _makeElevationUnknownCommand = new RelayCommand(async () => await MakeElevationUnknownAsync(), () => CanEditSelectedPiece);
    }

    public ObservableCollection<LedgerPieceRowViewModel> Pieces { get; } = new();

    public LedgerPieceRowViewModel? SelectedPiece
    {
        get => _selectedPiece;
        set
        {
            if (!SetProperty(ref _selectedPiece, value))
                return;

            if (value is not null)
            {
                // Pre-fill elevation input with the selected end's current known/provisional value when available.
                var display = SelectedElevationEnd == LedgerElevationEnd.Start ? value.StartElevation : value.EndElevation;
                ElevationValueText = display.NumericValue?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;
            }

            RaisePropertyChanged(nameof(CanEditSelectedPiece));
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

            if (SelectedPiece is not null)
            {
                var display = value == LedgerElevationEnd.Start ? SelectedPiece.StartElevation : SelectedPiece.EndElevation;
                ElevationValueText = display.NumericValue?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;
            }
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
        set => SetProperty(ref _elevationValueText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
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

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
                return;
            RaiseCanExecuteChanged();
        }
    }

    public bool CanEditSelectedPiece => SelectedPiece is not null && !IsBusy;

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
    /// </summary>
    public Func<LedgerElevationEditRequest, Task>? OnElevationEditRequested { get; set; }

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
        IssuesSummary = computedLedgers.IssueIds.Count == 0
            ? "No issues."
            : string.Join(", ", computedLedgers.IssueIds.Distinct(StringComparer.Ordinal));

        if (selectedId is not null)
            SelectedPiece = Pieces.FirstOrDefault(p => string.Equals(p.PieceId, selectedId, StringComparison.Ordinal));

        StatusMessage = $"Loaded {Pieces.Count} piece(s). Route/cut totals are separate ledgers; unknown is not zero.";
        RaisePropertyChanged(nameof(CanEditSelectedPiece));
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
            StatusMessage = "Select a piece before changing elevation.";
            return;
        }

        if (!TryParseElevation(ElevationValueText, out var elevation))
        {
            StatusMessage = "Enter a finite elevation value in meters (for example 12.5).";
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
            StatusMessage = "Select a piece before making elevation unknown.";
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
            StatusMessage = pendingMessage + " No application handler is wired; shell must set OnElevationEditRequested or replace command properties.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = pendingMessage;
            await OnElevationEditRequested(request).ConfigureAwait(true);
            StatusMessage = pendingMessage + " Completed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Elevation edit failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
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

    private void RaiseCanExecuteChanged()
    {
        if (_changeElevationCommand is RelayCommand change)
            change.RaiseCanExecuteChanged();
        if (_makeElevationUnknownCommand is RelayCommand makeUnknown)
            makeUnknown.RaiseCanExecuteChanged();
    }
}
