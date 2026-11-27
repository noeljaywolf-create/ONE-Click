using System.Linq;
using System.Threading;
using System.Windows;
using OneClick.Application.Commands;
using OneClick.Application.Services;
using OneClick.Core.Domain;
using OneClick.Core.TaggedValues;
using OneClick.Desktop.ViewModels;
using OneClick.Desktop.ViewModels.Geometry;
using OneClick.Desktop.ViewModels.Ledger;

namespace OneClick.Desktop;

public partial class MainWindow : Window
{
    private readonly GeometryViewModel _geometryViewModel = new();
    private bool _suppressSelectionSync;

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        Loaded += OnLoaded;
        ViewModel.AfterInitializeAsync = () => RefreshViewsAsync(ViewModel.StatusMessage);

        GeometryViewControl.DataContext = _geometryViewModel;
        GeometryViewControl.Pieces = _geometryViewModel.Pieces;
        GeometryViewControl.StatusText = _geometryViewModel.StatusText;

        _geometryViewModel.PropertyChanged += (_, e) =>
        {
            if (_suppressSelectionSync)
                return;

            if (e.PropertyName == nameof(GeometryViewModel.SelectedPieceId))
            {
                if (GeometryViewControl.SelectedPieceId != _geometryViewModel.SelectedPieceId)
                    GeometryViewControl.SelectedPieceId = _geometryViewModel.SelectedPieceId;

                var pieceId = _geometryViewModel.SelectedPieceId;
                var row = pieceId is null
                    ? null
                    : LedgerPanelControl.ViewModel.Pieces.FirstOrDefault(p => p.PieceId == pieceId);
                if (!ReferenceEquals(LedgerPanelControl.ViewModel.SelectedPiece, row))
                    LedgerPanelControl.ViewModel.SelectedPiece = row;
            }

            if (e.PropertyName == nameof(GeometryViewModel.StatusText))
            {
                GeometryViewControl.StatusText = _geometryViewModel.StatusText;
            }
        };

        System.ComponentModel.DependencyPropertyDescriptor
            .FromProperty(OneClick.Desktop.Controls.Geometry.GeometryView.SelectedPieceIdProperty, typeof(OneClick.Desktop.Controls.Geometry.GeometryView))
            ?.AddValueChanged(GeometryViewControl, (_, _) =>
            {
                if (_suppressSelectionSync)
                    return;

                if (_geometryViewModel.SelectedPieceId != GeometryViewControl.SelectedPieceId)
                    _geometryViewModel.SelectedPieceId = GeometryViewControl.SelectedPieceId;
            });

        LedgerPanelControl.ViewModel.PropertyChanged += (_, e) =>
        {
            if (_suppressSelectionSync)
                return;

            if (e.PropertyName == nameof(LedgerPanelViewModel.SelectedPiece))
            {
                var id = LedgerPanelControl.ViewModel.SelectedPiece?.PieceId;
                if (_geometryViewModel.SelectedPieceId != id)
                    _geometryViewModel.SelectedPieceId = id;
            }
        };

        LedgerPanelControl.ViewModel.OnElevationEditRequested = HandleElevationEditAsync;
        SnapshotPanelControl.ViewModel.FreezeAction = FreezeSnapshotAsync;
        SnapshotPanelControl.ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SnapshotPanelViewModel.StatusMessage))
            {
                ViewModel.StatusMessage = SnapshotPanelControl.ViewModel.StatusMessage;
                ViewModel.LastRefreshedDisplay = DateTimeOffset.Now.ToString("HH:mm:ss");
            }
        };
    }

    public MainViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        try
        {
            await ViewModel.InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Shell initialization failed: {ex.Message}";
        }
    }

    private async Task RefreshViewsAsync(string? statusMessage = null, bool updateSnapshotPanel = true)
    {
        if (ViewModel.DomainStateStore is null || ViewModel.CurrentProjectId is null)
        {
            ViewModel.StatusMessage = "Cannot refresh views: shell is not initialized.";
            return;
        }

        var projectId = ViewModel.CurrentProjectId.Value;
        var state = await ViewModel.DomainStateStore.GetAsync(projectId).ConfigureAwait(true);
        if (state is null)
        {
            ViewModel.StatusMessage = $"Cannot refresh views: project '{projectId.Value}' not found in domain state store.";
            return;
        }

        var selectedPieceId = _geometryViewModel.SelectedPieceId
            ?? LedgerPanelControl.ViewModel.SelectedPiece?.PieceId;

        _suppressSelectionSync = true;
        try
        {
            _geometryViewModel.LoadPieces(state.Pieces);
            GeometryViewControl.Pieces = _geometryViewModel.Pieces;
            GeometryViewControl.StatusText = _geometryViewModel.StatusText;

            LedgerPanelControl.ViewModel.Refresh(state);

            RestoreSelection(selectedPieceId);

            if (updateSnapshotPanel)
            {
                var snapshot = state.SnapshotManifests.LastOrDefault();
                if (snapshot is not null)
                {
                    SnapshotPanelControl.ViewModel.SetManifest(
                        snapshot,
                        $"Loaded snapshot '{snapshot.SnapshotId}' (revision {snapshot.ProjectRevision}) from project state.");
                }
            }
        }
        finally
        {
            _suppressSelectionSync = false;
        }

        ViewModel.SyncFromDomainState(state, BuildRefreshStatus(state, statusMessage));
    }

    private void RestoreSelection(string? selectedPieceId)
    {
        if (string.IsNullOrWhiteSpace(selectedPieceId))
        {
            _geometryViewModel.SelectedPieceId = null;
            if (LedgerPanelControl.ViewModel.SelectedPiece is not null)
                LedgerPanelControl.ViewModel.SelectedPiece = null;
            GeometryViewControl.SelectedPieceId = null;
            return;
        }

        var geometryPiece = _geometryViewModel.Pieces.FirstOrDefault(p => string.Equals(p.Id, selectedPieceId, StringComparison.Ordinal));
        var ledgerRow = LedgerPanelControl.ViewModel.Pieces.FirstOrDefault(p => string.Equals(p.PieceId, selectedPieceId, StringComparison.Ordinal));

        _geometryViewModel.SelectedPiece = geometryPiece;
        if (!ReferenceEquals(LedgerPanelControl.ViewModel.SelectedPiece, ledgerRow))
            LedgerPanelControl.ViewModel.SelectedPiece = ledgerRow;
        if (GeometryViewControl.SelectedPieceId != geometryPiece?.Id)
            GeometryViewControl.SelectedPieceId = geometryPiece?.Id;
    }

    private static string BuildRefreshStatus(ProjectDomainState state, string? statusMessage)
    {
        if (!string.IsNullOrWhiteSpace(statusMessage))
            return statusMessage;

        var snapshotText = state.SnapshotManifests.Count == 0
            ? "no snapshot manifests"
            : $"{state.SnapshotManifests.Count} snapshot manifest(s)";
        return $"Project '{state.ProjectId.Value}' at revision {state.Revision}: {state.Pieces.Count} piece(s), {snapshotText}.";
    }

    private async Task<string?> HandleElevationEditAsync(LedgerElevationEditRequest request)
    {
        if (ViewModel.CommandService is null || ViewModel.DomainStateStore is null || ViewModel.CurrentProjectId is null || ViewModel.ActorContext is null)
        {
            var notReady = "Cannot edit elevation: shell is not initialized.";
            ViewModel.StatusMessage = notReady;
            return notReady;
        }

        var projectId = ViewModel.CurrentProjectId.Value;
        var state = await ViewModel.DomainStateStore.GetAsync(projectId).ConfigureAwait(true);
        var piece = state?.Pieces.FirstOrDefault(p => p.Id == request.PieceId);
        if (state is null || piece is null)
        {
            var missing = $"Cannot edit elevation: piece '{request.PieceId}' not found in project '{projectId.Value}'.";
            ViewModel.StatusMessage = missing;
            return missing;
        }

        static double? ToNullableZ(TaggedValue tv) =>
            tv switch
            {
                KnownValue k => k.Value,
                ProvisionalValue p => p.Value,
                _ => null
            };

        var startZ = ToNullableZ(piece.StartElevation);
        var endZ = ToNullableZ(piece.EndElevation);
        double? newStartZ = startZ;
        double? newEndZ = endZ;

        if (request.End == LedgerElevationEnd.Start)
            newStartZ = request.MakeUnknown ? null : request.NewElevationMeters;
        else
            newEndZ = request.MakeUnknown ? null : request.NewElevationMeters;

        var command = new UpsertRouteCommand(
            RouteId: piece.Id,
            Vertices:
            [
                new RouteVertex(piece.StartM.X, piece.StartM.Y, newStartZ),
                new RouteVertex(piece.EndM.X, piece.EndM.Y, newEndZ),
            ],
            StartPortId: null,
            EndPortId: null,
            RouteNotes: request.MakeUnknown
                ? $"Make {request.End} elevation unknown"
                : $"Set {request.End} elevation to {request.NewElevationMeters:G17} m");

        try
        {
            var revisionState = await ViewModel.DomainStateStore.GetAsync(projectId).ConfigureAwait(true);
            var expectedRevision = new OneClick.Application.Contracts.ProjectRevision(revisionState?.Revision ?? 0);

            var receipt = await ViewModel.CommandService.ExecuteCommandAsync(
                projectId,
                expectedRevision,
                new OneClick.Application.Contracts.CommandId(Guid.NewGuid().ToString("N")),
                command,
                ViewModel.ActorContext).ConfigureAwait(true);

            var issueText = FormatIssueIds(receipt.IssueIds);
            var factualStatus = $"Elevation edit accepted for '{request.PieceId}' ({request.End}). Revision {receipt.NewRevision.Value}; command issues: {issueText}.";
            await RefreshViewsAsync(factualStatus).ConfigureAwait(true);
            ViewModel.StatusMessage = factualStatus;
            return factualStatus;
        }
        catch (Exception ex)
        {
            var errorStatus = $"Elevation edit failed for '{request.PieceId}' ({request.End}): {ex.Message}";
            ViewModel.StatusMessage = errorStatus;
            throw;
        }
    }

    private async Task<SnapshotManifest> FreezeSnapshotAsync(string snapshotId, string? label, CancellationToken cancellationToken)
    {
        if (ViewModel.DomainStateStore is null || ViewModel.CurrentProjectId is null)
            throw new InvalidOperationException("Shell is not initialized.");

        var projectId = ViewModel.CurrentProjectId.Value;
        var freezeService = new SnapshotFreezeService(ViewModel.DomainStateStore);
        var manifest = await freezeService.FreezeAsync(projectId, snapshotId, DateTimeOffset.UtcNow, label, cancellationToken: cancellationToken).ConfigureAwait(true);

        var issueText = FormatIssueIds(manifest.Issues);
        var exclusionText = manifest.Exclusions.Count == 0 ? "none" : $"{manifest.Exclusions.Count} ({string.Join(", ", manifest.Exclusions)})";
        var factualStatus = $"Snapshot '{manifest.SnapshotId}' frozen at revision {manifest.ProjectRevision}. Snapshot issues: {issueText}. Exclusions: {exclusionText}.";
        await RefreshViewsAsync(factualStatus, updateSnapshotPanel: false).ConfigureAwait(true);

        // Keep the frozen manifest visible; RefreshViewsAsync skipped snapshot panel update.
        SnapshotPanelControl.ViewModel.SetManifest(manifest, factualStatus);
        ViewModel.StatusMessage = factualStatus;
        return manifest;
    }

    private static string FormatIssueIds(IReadOnlyList<string> issueIds)
    {
        if (issueIds is null || issueIds.Count == 0)
            return "none";

        var distinct = issueIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return distinct.Length == 0
            ? "none"
            : $"{distinct.Length} [{string.Join(", ", distinct)}]";
    }
}