using OneClick.Core.Domain;
using OneClick.Core.Ids;
using OneClick.Application.Persistence;

namespace OneClick.Application.Services;

/// <summary>
/// Deterministic in-memory snapshot freeze service for the first build slice.
/// Freezes current domain state into an immutable SnapshotManifest and stores it in the domain state store.
/// </summary>
public sealed class SnapshotFreezeService
{
    private readonly IProjectDomainStateStore _domainStateStore;

    public SnapshotFreezeService(IProjectDomainStateStore domainStateStore)
    {
        _domainStateStore = domainStateStore ?? throw new ArgumentNullException(nameof(domainStateStore));
    }

    public async Task<SnapshotManifest> FreezeAsync(
        ProjectId projectId,
        string snapshotId,
        DateTimeOffset createdAtUtc,
        string? label = null,
        IReadOnlyList<string>? exclusions = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(projectId.Value))
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (string.IsNullOrWhiteSpace(snapshotId)) throw new ArgumentException("SnapshotId is required.", nameof(snapshotId));

        var state = await _domainStateStore.GetAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (state is null)
            throw new KeyNotFoundException($"Project '{projectId.Value}' was not found.");

        var manifest = ProjectSnapshotFreezer.Freeze(state, snapshotId, createdAtUtc, label, exclusions);
        state.AddSnapshotManifest(manifest);
        await _domainStateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        return manifest;
    }
}

