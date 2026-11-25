namespace OneClick.Core.Geometry;

/// <summary>
/// Immutable polyline with finite known 3D vertices in metres.
/// Length is the sum of straight 3D segment lengths; it is only meaningful because
/// every vertex carries a known finite Z.
/// </summary>
public sealed record Polyline3D
{
    private readonly IReadOnlyList<Point3D> _vertices;

    public Polyline3D(IReadOnlyList<Point3D> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices, nameof(vertices));
        var copy = new Point3D[vertices.Count];
        for (var i = 0; i < vertices.Count; i++)
            copy[i] = vertices[i];
        _vertices = Array.AsReadOnly(copy);
    }

    public IReadOnlyList<Point3D> Vertices => _vertices;

    public double Length
    {
        get
        {
            double total = 0d;
            for (var i = 1; i < _vertices.Count; i++)
                total = Guard.Finite(total + new LineSegment3D(_vertices[i - 1], _vertices[i]).Length, nameof(Length));
            return total;
        }
    }

    public bool Equals(Polyline3D? other) =>
        other is not null && _vertices.SequenceEqual(other._vertices);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var vertex in _vertices)
            hash.Add(vertex);
        return hash.ToHashCode();
    }
}
