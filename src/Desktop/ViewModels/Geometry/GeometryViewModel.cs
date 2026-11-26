using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OneClick.Core.Domain;

namespace OneClick.Desktop.ViewModels.Geometry;

/// <summary>
/// Bindable view model for the geometry plan view. Owns the displayed piece collection
/// and selected piece. This is a display model; it does not mutate engineering state.
/// </summary>
public sealed class GeometryViewModel : INotifyPropertyChanged
{
    private GeometryPieceViewModel? _selectedPiece;
    private string _statusText = "No geometry loaded.";

    public GeometryViewModel()
    {
        Pieces = new ObservableCollection<GeometryPieceViewModel>();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<GeometryPieceViewModel> Pieces { get; }

    public GeometryPieceViewModel? SelectedPiece
    {
        get => _selectedPiece;
        set
        {
            if (ReferenceEquals(_selectedPiece, value)) return;
            _selectedPiece = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedPieceId));
        }
    }

    public string? SelectedPieceId
    {
        get => SelectedPiece?.Id;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                SelectedPiece = null;
                return;
            }

            var match = Pieces.FirstOrDefault(p => string.Equals(p.Id, value, StringComparison.Ordinal));
            if (match is not null)
                SelectedPiece = match;
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (string.Equals(_statusText, value, StringComparison.Ordinal)) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Replaces the displayed pieces from domain route pieces. Selection is preserved by stable ID when possible.
    /// </summary>
    public void LoadPieces(IEnumerable<RoutePiece> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);

        var previousSelectedId = SelectedPiece?.Id;
        Pieces.Clear();

        foreach (var piece in pieces)
        {
            Pieces.Add(new GeometryPieceViewModel(piece));
        }

        SelectedPiece = previousSelectedId is null
            ? null
            : Pieces.FirstOrDefault(p => string.Equals(p.Id, previousSelectedId, StringComparison.Ordinal));

        var unknownCount = Pieces.Count(p => p.HasUnknownElevation);
        StatusText = Pieces.Count == 0
            ? "No geometry loaded."
            : unknownCount > 0
                ? $"{Pieces.Count} piece(s) loaded; {unknownCount} with unknown elevation."
                : $"{Pieces.Count} piece(s) loaded.";
    }

    public void Clear()
    {
        Pieces.Clear();
        SelectedPiece = null;
        StatusText = "No geometry loaded.";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
