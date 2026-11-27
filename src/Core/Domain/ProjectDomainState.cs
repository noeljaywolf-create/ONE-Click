using OneClick.Core.Ids;

namespace OneClick.Core.Domain;

/// <summary>
/// Minimal in-memory project domain state for the first build slice.
/// Holds project identity, revision, route/piece state, document/calibration metadata, and snapshot manifest placeholders.
/// No persistence, no UI, no DB.
/// </summary>
public sealed class ProjectDomainState
{
    private readonly Dictionary<string, RoutePiece> _piecesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImportedDocumentRecord> _documentsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DocumentRegionRecord> _regionsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CalibrationRecord> _calibrationsByRegionId = new(StringComparer.Ordinal);
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

    /// <summary>Imported document metadata records. No PDF content/parsing.</summary>
    public IReadOnlyCollection<ImportedDocumentRecord> ImportedDocuments => _documentsById.Values;

    /// <summary>Document region identities created during import. Calibration references these.</summary>
    public IReadOnlyCollection<DocumentRegionRecord> Regions => _regionsById.Values;

    /// <summary>Calibration metadata records keyed by region ID. No calibration math.</summary>
    public IReadOnlyCollection<CalibrationRecord> Calibrations => _calibrationsByRegionId.Values;

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

    public bool TryGetImportedDocument(string documentId, out ImportedDocumentRecord? document)
    {
        Guard.Required(documentId, nameof(documentId));
        return _documentsById.TryGetValue(documentId, out document);
    }

    public void UpsertImportedDocument(ImportedDocumentRecord document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _documentsById[document.DocumentId] = document;
    }

    public bool TryGetRegion(string regionId, out DocumentRegionRecord? region)
    {
        Guard.Required(regionId, nameof(regionId));
        return _regionsById.TryGetValue(regionId, out region);
    }

    public void UpsertRegion(DocumentRegionRecord region)
    {
        ArgumentNullException.ThrowIfNull(region);
        _regionsById[region.RegionId] = region;
    }

    public bool TryGetCalibrationForRegion(string regionId, out CalibrationRecord? calibration)
    {
        Guard.Required(regionId, nameof(regionId));
        return _calibrationsByRegionId.TryGetValue(regionId, out calibration);
    }

    public void UpsertCalibration(CalibrationRecord calibration)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        _calibrationsByRegionId[calibration.RegionId] = calibration;
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
            _snapshotManifests.ToArray(),
            _documentsById.Values.OrderBy(d => d.DocumentId, StringComparer.Ordinal).ToArray(),
            _regionsById.Values.OrderBy(r => r.RegionId, StringComparer.Ordinal).ToArray(),
            _calibrationsByRegionId.Values.OrderBy(c => c.CalibrationId, StringComparer.Ordinal).ToArray());
    }
}

/// <summary>
/// Detached read model for snapshot/export hooks. Immutable point-in-time view.
/// </summary>
public sealed record ProjectDomainSnapshot
{
    public ProjectDomainSnapshot(
        ProjectId projectId,
        long revision,
        IReadOnlyList<RoutePiece> pieces,
        IReadOnlyList<SnapshotManifest> snapshotManifests,
        IReadOnlyList<ImportedDocumentRecord>? importedDocuments = null,
        IReadOnlyList<DocumentRegionRecord>? regions = null,
        IReadOnlyList<CalibrationRecord>? calibrations = null)
    {
        ProjectId = projectId;
        Revision = revision;
        Pieces = pieces ?? Array.Empty<RoutePiece>();
        SnapshotManifests = snapshotManifests ?? Array.Empty<SnapshotManifest>();
        ImportedDocuments = importedDocuments ?? Array.Empty<ImportedDocumentRecord>();
        Regions = regions ?? Array.Empty<DocumentRegionRecord>();
        Calibrations = calibrations ?? Array.Empty<CalibrationRecord>();
    }

    public ProjectId ProjectId { get; init; }
    public long Revision { get; init; }
    public IReadOnlyList<RoutePiece> Pieces { get; init; }
    public IReadOnlyList<SnapshotManifest> SnapshotManifests { get; init; }
    public IReadOnlyList<ImportedDocumentRecord> ImportedDocuments { get; init; }
    public IReadOnlyList<DocumentRegionRecord> Regions { get; init; }
    public IReadOnlyList<CalibrationRecord> Calibrations { get; init; }
}
