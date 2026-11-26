using System.Linq;
using System.Windows;
using OneClick.Application.Commands;
using OneClick.Application.Services;
using OneClick.Core.Domain;
using OneClick.Desktop.ViewModels;
using OneClick.Desktop.ViewModels.Geometry;
using OneClick.Desktop.ViewModels.Ledger;

namespace OneClick.Desktop;

public partial class MainWindow : Window
{
    private readonly GeometryViewModel _geometryViewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        Loaded += OnLoaded;

        GeometryViewControl.DataContext = _geometryViewModel;
        GeometryViewControl.Pieces = _geometryViewModel.Pieces;
        GeometryViewControl.StatusText = _geometryViewModel.StatusText;

        _geometryViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GeometryViewModel.SelectedPieceId))
            {
                if (GeometryViewControl.SelectedPieceId != _geometryViewModel.SelectedPieceId)
                    GeometryViewControl.SelectedPieceId = _geometryViewModel.SelectedPieceId;

                var pieceId = _geometryViewModel.SelectedPieceId;
                if (pieceId is not null)
                {
                    var row = LedgerPanelControl.ViewModel.Pieces.FirstOrDefault(p => p.PieceId == pieceId);
                    if (row is not null)
                        LedgerPanelControl.ViewModel.SelectedPiece = row;
                }
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
                _geometryViewModel.SelectedPieceId = GeometryViewControl.SelectedPieceId;
            });

        LedgerPanelControl.ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LedgerPanelViewModel.SelectedPiece))
            {
                var id = LedgerPanelControl.ViewModel.SelectedPiece?.PieceId;
                if (_geometryViewModel.SelectedPieceId != id)
                    _geometryViewModel.SelectedPieceId = id;
            }
        };

        LedgerPanelControl.ViewModel.OnElevationEditRequested = HandleElevationEditAsync;

        SnapshotPanelControl.ViewModel.FreezeAction = FreezeSnapshotAsync;
    }

    public MainViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.InitializeAsync();
        await RefreshViewsAsync();
    }

    private async Task RefreshViewsAsync()
    {
        if (ViewModel.DomainStateStore is null || ViewModel.CurrentProjectId is null)
            return;

        var projectId = ViewModel.CurrentProjectId!.Value;
        var state = await ViewModel.DomainStateStore.GetAsync(projectId);
        if (state is null)
            return;

        _geometryViewModel.LoadPieces(state.Pieces);
        GeometryViewControl.Pieces = _geometryViewModel.Pieces;
        GeometryViewControl.StatusText = _geometryViewModel.StatusText;

        LedgerPanelControl.ViewModel.Refresh(state);

        var snapshot = state.SnapshotManifests.LastOrDefault();
        if (snapshot is not null)
            SnapshotPanelControl.ViewModel.SetManifest(snapshot, $"Loaded snapshot '{snapshot.SnapshotId}' from project state.");
    }

    private async Task HandleElevationEditAsync(LedgerElevationEditRequest request)
    {
        if (ViewModel.CommandService is null || ViewModel.DomainStateStore is null || ViewModel.CurrentProjectId is null || ViewModel.ActorContext is null)
        {
            ViewModel.StatusMessage = "Cannot edit elevation: shell is not initialized.";
            return;
        }

        var projectId = ViewModel.CurrentProjectId!.Value;
        var state = await ViewModel.DomainStateStore.GetAsync(projectId);
        var piece = state?.Pieces.FirstOrDefault(p => p.Id == request.PieceId);
        if (state is null || piece is null)
        {
            ViewModel.StatusMessage = $"Cannot edit elevation: piece '{request.PieceId}' not found.";
            return;
        }

        static double? ToNullableZ(OneClick.Core.TaggedValues.TaggedValue tv) =>
            tv switch
            {
                OneClick.Core.TaggedValues.KnownValue k => k.Value,
                OneClick.Core.TaggedValues.ProvisionalValue p => p.Value,
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
            var revision = await ViewModel.DomainStateStore.GetAsync(projectId);
            var expectedRevision = new OneClick.Application.Contracts.ProjectRevision(revision?.Revision ?? 0);

            var receipt = await ViewModel.CommandService.ExecuteCommandAsync(
                projectId,
                expectedRevision,
                new OneClick.Application.Contracts.CommandId(Guid.NewGuid().ToString("N")),
                command,
                ViewModel.ActorContext);

            ViewModel.ProjectRevision = receipt.NewRevision.Value;
            ViewModel.StatusMessage = $"UpsertRoute accepted. Revision {receipt.NewRevision.Value}. Issues: {receipt.IssueIds.Count}.";
            await RefreshViewsAsync();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Elevation edit failed: {ex.Message}";
            throw;
        }
    }

    private async Task<SnapshotManifest> FreezeSnapshotAsync(string snapshotId, string? label, CancellationToken cancellationToken)
    {
        if (ViewModel.DomainStateStore is null || ViewModel.CurrentProjectId is null)
            throw new InvalidOperationException("Shell is not initialized.");

        var projectId = ViewModel.CurrentProjectId!.Value;
        var freezeService = new SnapshotFreezeService(ViewModel.DomainStateStore);
        var manifest = await freezeService.FreezeAsync(projectId, snapshotId, DateTimeOffset.UtcNow, label, cancellationToken: cancellationToken);
        ViewModel.SnapshotManifestCount = (await ViewModel.DomainStateStore.GetAsync(projectId))?.SnapshotManifests.Count ?? ViewModel.SnapshotManifestCount;
        ViewModel.StatusMessage = $"Snapshot '{manifest.SnapshotId}' frozen at revision {manifest.ProjectRevision}.";
        return manifest;
    }
}