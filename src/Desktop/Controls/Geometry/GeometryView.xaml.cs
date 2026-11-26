using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OneClick.Desktop.ViewModels.Geometry;

namespace OneClick.Desktop.Controls.Geometry;

/// <summary>
/// Simple 2D plan-view renderer for route pieces. This is a visualization control only;
/// it does not perform engineering calculations, infer undeclared parts, or mutate domain state.
/// </summary>
public partial class GeometryView : UserControl
{
    public static readonly DependencyProperty PiecesProperty = DependencyProperty.Register(
        nameof(Pieces),
        typeof(IEnumerable<GeometryPieceViewModel>),
        typeof(GeometryView),
        new PropertyMetadata(null, OnPiecesChanged));

    public static readonly DependencyProperty SelectedPieceProperty = DependencyProperty.Register(
        nameof(SelectedPiece),
        typeof(GeometryPieceViewModel),
        typeof(GeometryView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedPieceChanged));

    public static readonly DependencyProperty SelectedPieceIdProperty = DependencyProperty.Register(
        nameof(SelectedPieceId),
        typeof(string),
        typeof(GeometryView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedPieceIdChanged));

    public static readonly DependencyProperty ShowLabelsProperty = DependencyProperty.Register(
        nameof(ShowLabels),
        typeof(bool),
        typeof(GeometryView),
        new PropertyMetadata(true, OnRenderPropertyChanged));

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText),
        typeof(string),
        typeof(GeometryView),
        new PropertyMetadata("No geometry loaded."));

    private static readonly Brush KnownBrush = CreateFrozenBrush(Color.FromRgb(0x2E, 0x6D, 0xA4));
    private static readonly Brush UnknownBrush = CreateFrozenBrush(Color.FromRgb(0xCC, 0x7A, 0x00));
    private static readonly Brush SelectedBrush = CreateFrozenBrush(Color.FromRgb(0x1F, 0x8F, 0x3A));
    private static readonly Brush LabelBrush = CreateFrozenBrush(Color.FromRgb(0x33, 0x33, 0x33));
    private static readonly Brush UnknownLabelBrush = CreateFrozenBrush(Color.FromRgb(0x8A, 0x4B, 0x00));

    private readonly Dictionary<string, GeometryPieceViewModel> _pieceById = new(StringComparer.Ordinal);
    private INotifyCollectionChanged? _observedCollection;
    private bool _syncingSelection;

    public GeometryView()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    public IEnumerable<GeometryPieceViewModel>? Pieces
    {
        get => (IEnumerable<GeometryPieceViewModel>?)GetValue(PiecesProperty);
        set => SetValue(PiecesProperty, value);
    }

    public GeometryPieceViewModel? SelectedPiece
    {
        get => (GeometryPieceViewModel?)GetValue(SelectedPieceProperty);
        set => SetValue(SelectedPieceProperty, value);
    }

    public string? SelectedPieceId
    {
        get => (string?)GetValue(SelectedPieceIdProperty);
        set => SetValue(SelectedPieceIdProperty, value);
    }

    public bool ShowLabels
    {
        get => (bool)GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    private static void OnPiecesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (GeometryView)d;
        view.DetachCollectionChanged(e.OldValue as INotifyCollectionChanged);
        view.AttachCollectionChanged(e.NewValue as INotifyCollectionChanged);
        view.RebuildPieceIndex();
        view.SyncSelectionFromId();
        view.Render();
    }

    private static void OnSelectedPieceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (GeometryView)d;
        if (view._syncingSelection) return;
        view._syncingSelection = true;
        try
        {
            var piece = e.NewValue as GeometryPieceViewModel;
            view.SelectedPieceId = piece?.Id;
        }
        finally
        {
            view._syncingSelection = false;
        }

        view.UpdateSelectionText();
        view.Render();
    }

    private static void OnSelectedPieceIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (GeometryView)d;
        if (view._syncingSelection) return;
        view.SyncSelectionFromId();
    }

    private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((GeometryView)d).Render();
    }

    private void AttachCollectionChanged(INotifyCollectionChanged? collection)
    {
        if (collection is null) return;
        _observedCollection = collection;
        _observedCollection.CollectionChanged += Pieces_CollectionChanged;
    }

    private void DetachCollectionChanged(INotifyCollectionChanged? collection)
    {
        if (collection is null) return;
        collection.CollectionChanged -= Pieces_CollectionChanged;
        if (ReferenceEquals(_observedCollection, collection))
            _observedCollection = null;
    }

    private void Pieces_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildPieceIndex();
        SyncSelectionFromId();
        Render();
    }

    private void RebuildPieceIndex()
    {
        _pieceById.Clear();
        if (Pieces is null) return;
        foreach (var piece in Pieces)
        {
            if (piece is null || string.IsNullOrWhiteSpace(piece.Id)) continue;
            _pieceById[piece.Id] = piece;
        }
    }

    private void SyncSelectionFromId()
    {
        if (_syncingSelection) return;
        _syncingSelection = true;
        try
        {
            var id = SelectedPieceId;
            if (string.IsNullOrWhiteSpace(id))
            {
                if (SelectedPiece is not null) SelectedPiece = null;
            }
            else if (_pieceById.TryGetValue(id, out var match))
            {
                if (!ReferenceEquals(SelectedPiece, match))
                    SelectedPiece = match;
            }
            else
            {
                // Keep SelectedPieceId if caller set it before pieces loaded? No - clear stale selection.
                SelectedPiece = null;
                SelectedPieceId = null;
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        UpdateSelectionText();
        Render();
    }

    private void UpdateSelectionText()
    {
        var selected = SelectedPiece;
        if (selected is null)
        {
            SelectionText.Text = "Selected: none";
            return;
        }

        SelectionText.Text = $"Selected: {selected.Id} [{selected.Group}] - {selected.ElevationStatus}";
    }

    private void DrawingCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Render();
    }

    private void DrawingCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(DrawingCanvas);
        var hitId = FindNearestPieceId(position, tolerance: 8.0);
        if (hitId is null) return;
        if (_pieceById.TryGetValue(hitId, out var piece))
            SelectedPiece = piece;
    }

    private string? FindNearestPieceId(Point point, double tolerance)
    {
        var pieces = Pieces?.Where(p => p is not null).ToList();
        if (pieces is null || pieces.Count == 0) return null;
        if (!TryGetTransform(pieces, DrawingCanvas.ActualWidth, DrawingCanvas.ActualHeight, out var transform))
            return null;

        string? bestId = null;
        var bestDistance = tolerance;
        foreach (var piece in pieces)
        {
            var a = WorldToScreen(piece.StartX, piece.StartY, transform);
            var b = WorldToScreen(piece.EndX, piece.EndY, transform);
            var distance = DistanceToSegment(point, a, b);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                bestId = piece.Id;
            }
        }

        return bestId;
    }

    private void Render()
    {
        DrawingCanvas.Children.Clear();

        var pieces = Pieces?.Where(p => p is not null).ToList();
        if (pieces is null || pieces.Count == 0)
        {
            EmptyStateText.Visibility = Visibility.Visible;
            UpdateSelectionText();
            SetCurrentValue(StatusTextProperty, "No geometry loaded.");
            return;
        }

        EmptyStateText.Visibility = Visibility.Collapsed;
        UpdateSelectionText();

        var unknownCount = pieces.Count(p => p.HasUnknownElevation);
        var provisionalCount = pieces.Count(p => p.HasProvisionalElevation && !p.HasUnknownElevation);
        var status = $"{pieces.Count} piece(s)";
        if (unknownCount > 0) status += $", {unknownCount} unknown Z";
        if (provisionalCount > 0) status += $", {provisionalCount} provisional Z";
        SetCurrentValue(StatusTextProperty, status);

        if (!TryGetTransform(pieces, DrawingCanvas.ActualWidth, DrawingCanvas.ActualHeight, out var transform))
            return;

        foreach (var piece in pieces.OrderBy(p => string.Equals(p.Id, SelectedPieceId, StringComparison.Ordinal) ? 1 : 0))
        {
            DrawPiece(piece, transform);
        }
    }

    private void DrawPiece(GeometryPieceViewModel piece, RenderTransformData transform)
    {
        var a = WorldToScreen(piece.StartX, piece.StartY, transform);
        var b = WorldToScreen(piece.EndX, piece.EndY, transform);
        var isSelected = string.Equals(piece.Id, SelectedPieceId, StringComparison.Ordinal);
        var hasUnknownOrProvisional = piece.HasUnknownElevation || piece.HasProvisionalElevation;

        var stroke = isSelected ? SelectedBrush : hasUnknownOrProvisional ? UnknownBrush : KnownBrush;
        var thickness = isSelected ? 3.0 : 2.0;

        var line = new Line
        {
            X1 = a.X,
            Y1 = a.Y,
            X2 = b.X,
            Y2 = b.Y,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Tag = piece.Id,
            ToolTip = piece.ToolTip,
            SnapsToDevicePixels = true,
        };

        if (hasUnknownOrProvisional && !isSelected)
        {
            line.StrokeDashArray = piece.HasUnknownElevation ? new DoubleCollection { 3, 2 } : new DoubleCollection { 1, 2 };
        }

        DrawingCanvas.Children.Add(line);

        DrawEndpoint(a, stroke, isSelected ? 4.0 : 3.0, piece.ToolTip);
        DrawEndpoint(b, stroke, isSelected ? 4.0 : 3.0, piece.ToolTip);

        if (ShowLabels)
        {
            var mid = new Point((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
            var label = new TextBlock
            {
                Text = hasUnknownOrProvisional ? $"{piece.Id} !" : piece.Id,
                FontSize = 11,
                Foreground = hasUnknownOrProvisional ? UnknownLabelBrush : LabelBrush,
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                Padding = new Thickness(2, 0, 2, 0),
                IsHitTestVisible = false,
                ToolTip = piece.ToolTip,
            };
            Canvas.SetLeft(label, mid.X + 4);
            Canvas.SetTop(label, mid.Y - 14);
            DrawingCanvas.Children.Add(label);
        }
    }

    private void DrawEndpoint(Point point, Brush brush, double radius, string toolTip)
    {
        var dot = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = Brushes.White,
            Stroke = brush,
            StrokeThickness = 1.2,
            ToolTip = toolTip,
            Tag = "endpoint",
        };
        Canvas.SetLeft(dot, point.X - radius);
        Canvas.SetTop(dot, point.Y - radius);
        DrawingCanvas.Children.Add(dot);
    }

    private static bool TryGetTransform(IReadOnlyList<GeometryPieceViewModel> pieces, double width, double height, out RenderTransformData transform)
    {
        transform = default;
        if (width <= 10 || height <= 10 || pieces.Count == 0) return false;

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        foreach (var piece in pieces)
        {
            Include(piece.StartX, piece.StartY, ref minX, ref minY, ref maxX, ref maxY);
            Include(piece.EndX, piece.EndY, ref minX, ref minY, ref maxX, ref maxY);
        }

        if (!double.IsFinite(minX) || !double.IsFinite(minY) || !double.IsFinite(maxX) || !double.IsFinite(maxY))
            return false;

        var spanX = Math.Max(maxX - minX, 0.001);
        var spanY = Math.Max(maxY - minY, 0.001);
        const double padding = 24.0;
        var usableWidth = Math.Max(10.0, width - padding * 2);
        var usableHeight = Math.Max(10.0, height - padding * 2);
        var scale = Math.Min(usableWidth / spanX, usableHeight / spanY);

        // Center content within available space.
        var contentWidth = spanX * scale;
        var contentHeight = spanY * scale;
        var offsetX = padding + (usableWidth - contentWidth) / 2.0;
        var offsetY = padding + (usableHeight - contentHeight) / 2.0;

        transform = new RenderTransformData(minX, minY, maxX, maxY, scale, offsetX, offsetY);
        return true;
    }

    private static void Include(double x, double y, ref double minX, ref double minY, ref double maxX, ref double maxY)
    {
        if (x < minX) minX = x;
        if (y < minY) minY = y;
        if (x > maxX) maxX = x;
        if (y > maxY) maxY = y;
    }

    private static Point WorldToScreen(double x, double y, RenderTransformData transform)
    {
        // Plan view: world +Y up, canvas +Y down.
        var sx = transform.OffsetX + (x - transform.MinX) * transform.Scale;
        var sy = transform.OffsetY + (transform.MaxY - y) * transform.Scale;
        return new Point(sx, sy);
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= double.Epsilon)
        {
            var ddx = p.X - a.X;
            var ddy = p.Y - a.Y;
            return Math.Sqrt(ddx * ddx + ddy * ddy);
        }

        var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared;
        t = Math.Clamp(t, 0.0, 1.0);
        var px = a.X + t * dx;
        var py = a.Y + t * dy;
        var rx = p.X - px;
        var ry = p.Y - py;
        return Math.Sqrt(rx * rx + ry * ry);
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private readonly record struct RenderTransformData(
        double MinX,
        double MinY,
        double MaxX,
        double MaxY,
        double Scale,
        double OffsetX,
        double OffsetY);
}
