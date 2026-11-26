using OneClick.Core.Ids;

namespace OneClick.Core.Domain;

/// <summary>
/// Minimal in-memory project domain state for the first build slice.
/// Holds project identity, revision, route/piece state, and snapshot manifest placeholders.
/// No persistence, no UI, no DB.
/// </summary>
public sealed class ProjectDomainState
{
    private readonly Dictionary<string, RoutePiece> _piecesById = new(StringComparer.Ordinal);
    private readonly List<SnapshotManifest> _snapshotManifests = new();

    public ProjectDomainState(ProjectId projectId, long revision = 0)
    {
        if (string.IsNullOrWhiteSpace(projectId.Value))
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (revision < 0)
            throw new ArgumentOutOfRangeException(nameof(revision), revision, "Revision must be >= 0.");

        ProjectId = projectId;
        Revision = revision;
    }

    public ProjectId ProjectId { get; }
    public long Revision { get; private set; }

    /// <summary>Route/piece records for the slice. Keys are stable piece IDs.</summary>
    public IReadOnlyCollection<RoutePiece> Pieces => _piecesById.Values;

    /// <summary>Snapshot manifest placeholders/frozen manifest records created for this project.</summary>
    public IReadOnlyList<SnapshotManifest> SnapshotManifests => _snapshotManifests;

    public bool TryGetPiece(string pieceId, out RoutePiece? piece)
    {
        Guard.Required(pieceId, nameof(pieceId));
        return _piecesById.TryGetValue(pieceId, out piece);
    }

    public RoutePiece GetRequiredPiece(string pieceId)
    {
        if (!TryGetPiece(pieceId, out var piece) || piece is null)
            throw new KeyNotFoundException($"Piece '{pieceId}' was not found in project '{ProjectId.Value}'.");
        return piece;
    }

    public void UpsertPiece(RoutePiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        _piecesById[piece.Id] = piece;
    }

    public void ReplacePieces(IEnumerable<RoutePiece> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        _piecesById.Clear();
        foreach (var piece in pieces)
        {
            ArgumentNullException.ThrowIfNull(piece);
            if (!_piecesById.TryAdd(piece.Id, piece))
                throw new InvalidOperationException($"Duplicate piece id '{piece.Id}' in project '{ProjectId.Value}'.");
        }
    }

    public void SetRevision(long revision)
    {
        if (revision < 0)
            throw new ArgumentOutOfRangeException(nameof(revision), revision, "Revision must be >= 0.");
        Revision = revision;
    }

    public void AddSnapshotManifest(SnapshotManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!string.Equals(manifest.ProjectId, ProjectId.Value, StringComparison.Ordinal))
            throw new InvalidOperationException($"Snapshot manifest project '{manifest.ProjectId}' does not match '{ProjectId.Value}'.");
        if (_snapshotManifests.Any(m => string.Equals(m.SnapshotId, manifest.SnapshotId, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Snapshot manifest '{manifest.SnapshotId}' already exists in project '{ProjectId.Value}'.");
        _snapshotManifests.Add(manifest);
    }

    /// <summary>
    /// Creates a detached read model copy for snapshot/export hooks. Collections are copied.
    /// </summary>
    public ProjectDomainSnapshot ToSnapshot()
    {
        return new ProjectDomainSnapshot(
            ProjectId,
            Revision,
            _piecesById.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray(),
            _snapshotManifests.ToArray());
    }
}

/// <summary>
/// Detached read model for snapshot/export hooks. Immutable point-in-time view.
/// </summary>
public sealed record ProjectDomainSnapshot(
    ProjectId ProjectId,
    long Revision,
    IReadOnlyList<RoutePiece> Pieces,
    IReadOnlyList<SnapshotManifest> SnapshotManifests);
