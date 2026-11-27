using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using OneClick.Application.Services;
using OneClick.Core.Domain;

namespace OneClick.Desktop.ViewModels;

/// <summary>
/// ViewModel for the snapshot/export panel. Exposes bindable commands and callbacks so the shell
/// can wire freeze/export behavior to application services without the control owning service creation.
/// </summary>
public sealed class SnapshotPanelViewModel : ViewModelBase
{
    private SnapshotManifest? _manifest;
    private string _snapshotIdInput = "snap-001";
    private string _labelInput = string.Empty;
    private string _exportPath = string.Empty;
    private string _statusMessage = "No frozen snapshot. Freeze creates an immutable snapshot manifest.";
    private bool _isBusy;
    private bool _isFrozen;
    private bool _hasStatusError;
    private bool _hasManifestIssues;
    private bool _hasManifestExclusions;

    public SnapshotPanelViewModel()
    {
        FreezeCommand = new SnapshotDelegateCommand(FreezeAsync, () => !IsBusy, OnCommandError);
        ExportJsonCommand = new SnapshotDelegateCommand(ExportJsonAsync, () => !IsBusy && Manifest is not null, OnCommandError);
        ExportCsvCommand = new SnapshotDelegateCommand(ExportCsvAsync, () => !IsBusy && Manifest is not null, OnCommandError);
        BrowseExportJsonCommand = new SnapshotDelegateCommand(_ => BrowseExport("json"), _ => !IsBusy && Manifest is not null);
        BrowseExportCsvCommand = new SnapshotDelegateCommand(_ => BrowseExport("csv"), _ => !IsBusy && Manifest is not null);

        SourceRefs = new ObservableCollection<string>();
        Issues = new ObservableCollection<string>();
        Exclusions = new ObservableCollection<string>();
        KnownQuantityLines = new ObservableCollection<SnapshotQuantityLineDisplay>();
        ProvisionalQuantityLines = new ObservableCollection<SnapshotQuantityLineDisplay>();
        UnknownQuantityLines = new ObservableCollection<SnapshotQuantityLineDisplay>();
    }



    /// <summary>Shell wiring: performs freeze and returns the frozen manifest. The shell should call SnapshotFreezeService.</summary>
    public Func<string, string?, CancellationToken, Task<SnapshotManifest>>? FreezeAction { get; set; }

    /// <summary>Shell wiring: optional export handler. If null, the panel writes the artifact to the selected path.</summary>
    public Func<SnapshotManifest, SnapshotExportArtifact, string, CancellationToken, Task>? SaveExportAction { get; set; }

    /// <summary>View/shell wiring: prompts for a save path. Args: suggestedName, filter, title. Returns selected path or null.</summary>
    public Func<string, string, string, string?>? PickSaveFile { get; set; }

    public ICommand FreezeCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand BrowseExportJsonCommand { get; }
    public ICommand BrowseExportCsvCommand { get; }

    public SnapshotManifest? Manifest
    {
        get => _manifest;
        private set
        {
            if (ReferenceEquals(_manifest, value)) return;
            _manifest = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasManifest));
            OnPropertyChanged(nameof(SnapshotIdDisplay));
            OnPropertyChanged(nameof(ProjectRevisionDisplay));
            OnPropertyChanged(nameof(CreatedAtUtcDisplay));
            OnPropertyChanged(nameof(LabelDisplay));
            OnPropertyChanged(nameof(MethodVersionDisplay));
            OnPropertyChanged(nameof(SourceRefsDisplay));
            OnPropertyChanged(nameof(KnownCountDisplay));
            OnPropertyChanged(nameof(ProvisionalCountDisplay));
            OnPropertyChanged(nameof(UnknownCountDisplay));
            OnPropertyChanged(nameof(KnownTotalsDisplay));
            OnPropertyChanged(nameof(ProvisionalTotalsDisplay));
            OnPropertyChanged(nameof(UnknownTotalsDisplay));
            OnPropertyChanged(nameof(IssuesDisplay));
            OnPropertyChanged(nameof(ExclusionsDisplay));
            OnPropertyChanged(nameof(IssuesSummaryLabel));
            OnPropertyChanged(nameof(ExclusionsSummaryLabel));
            RefreshCollections();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool HasManifest => Manifest is not null;

    public bool IsFrozen
    {
        get => _isFrozen;
        private set
        {
            if (_isFrozen == value) return;
            _isFrozen = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FrozenLabel));
        }
    }

    public string FrozenLabel => IsFrozen ? "FROZEN SNAPSHOT (immutable)" : "Not frozen";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string SnapshotIdInput
    {
        get => _snapshotIdInput;
        set { if (_snapshotIdInput == value) return; _snapshotIdInput = value; OnPropertyChanged(); }
    }

    public string LabelInput
    {
        get => _labelInput;
        set { if (_labelInput == value) return; _labelInput = value; OnPropertyChanged(); }
    }

    public string ExportPath
    {
        get => _exportPath;
        set { if (_exportPath == value) return; _exportPath = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (_statusMessage == value) return; _statusMessage = value; OnPropertyChanged(); }
    }

    public bool HasStatusError
    {
        get => _hasStatusError;
        private set { if (_hasStatusError == value) return; _hasStatusError = value; OnPropertyChanged(); }
    }

    private void SetStatus(string message, bool isError = false)
    {
        HasStatusError = isError;
        StatusMessage = message;
    }

    public ObservableCollection<string> SourceRefs { get; }
    public ObservableCollection<string> Issues { get; }
    public ObservableCollection<string> Exclusions { get; }
    public ObservableCollection<SnapshotQuantityLineDisplay> KnownQuantityLines { get; }
    public ObservableCollection<SnapshotQuantityLineDisplay> ProvisionalQuantityLines { get; }
    public ObservableCollection<SnapshotQuantityLineDisplay> UnknownQuantityLines { get; }

    public string SnapshotIdDisplay => Manifest?.SnapshotId ?? "(none)";
    public string ProjectRevisionDisplay => Manifest is null ? "(none)" : Manifest.ProjectRevision.ToString(CultureInfo.InvariantCulture);
    public string CreatedAtUtcDisplay => Manifest is null ? "(none)" : Manifest.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture);
    public string LabelDisplay => string.IsNullOrWhiteSpace(Manifest?.Label) ? "(no label)" : Manifest!.Label!;
    public string MethodVersionDisplay => string.IsNullOrWhiteSpace(Manifest?.MethodVersion) ? "(not specified)" : Manifest!.MethodVersion!;
    public string SourceRefsDisplay => Manifest is null || Manifest.SourceRefs.Count == 0 ? "(none)" : string.Join(", ", Manifest.SourceRefs);
    public string IssuesDisplay => Manifest is null ? "(none)" : (Manifest.Issues.Count == 0 ? "(none)" : string.Join(Environment.NewLine, Manifest.Issues));
    public string ExclusionsDisplay => Manifest is null ? "(none)" : (Manifest.Exclusions.Count == 0 ? "(none)" : string.Join(Environment.NewLine, Manifest.Exclusions));
    public bool HasManifestIssues
    {
        get => _hasManifestIssues;
        private set { if (_hasManifestIssues == value) return; _hasManifestIssues = value; OnPropertyChanged(); OnPropertyChanged(nameof(IssuesSummaryLabel)); }
    }

    public bool HasManifestExclusions
    {
        get => _hasManifestExclusions;
        private set { if (_hasManifestExclusions == value) return; _hasManifestExclusions = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExclusionsSummaryLabel)); }
    }

    public string IssuesSummaryLabel => Manifest is null ? "Issues" : $"Issues ({Manifest.Issues.Count})";
    public string ExclusionsSummaryLabel => Manifest is null ? "Exclusions" : $"Exclusions ({Manifest.Exclusions.Count})";

    public string KnownCountDisplay => Manifest is null ? "(none)" : Manifest.KnownQuantities.Count.ToString(CultureInfo.InvariantCulture);
    public string ProvisionalCountDisplay => Manifest is null ? "(none)" : Manifest.ProvisionalQuantities.Count.ToString(CultureInfo.InvariantCulture);
    public string UnknownCountDisplay => Manifest is null ? "(none)" : Manifest.UnknownQuantities.Count.ToString(CultureInfo.InvariantCulture);

    public string KnownTotalsDisplay => FormatTotals(Manifest?.KnownQuantities, SnapshotQuantityStatus.Known);
    public string ProvisionalTotalsDisplay => FormatTotals(Manifest?.ProvisionalQuantities, SnapshotQuantityStatus.Provisional);
    public string UnknownTotalsDisplay => FormatTotals(Manifest?.UnknownQuantities, SnapshotQuantityStatus.Unknown);

    public void SetManifest(SnapshotManifest? manifest, string? statusMessage = null)
    {
        Manifest = manifest;
        IsFrozen = manifest is not null;
        if (!string.IsNullOrWhiteSpace(statusMessage))
            SetStatus(statusMessage!);
        else if (manifest is not null)
            SetStatus($"Frozen snapshot '{manifest.SnapshotId}' loaded. Manifest is immutable; exports preserve unknown as unknown.");
        else
            SetStatus("No frozen snapshot. Freeze creates an immutable snapshot manifest.");
    }

    public async Task ExportJsonToPathAsync(string path)
    {
        if (Manifest is null) return;
        await ExportArtifactAsync(path, SnapshotExportService.ExportJson(Manifest)).ConfigureAwait(true);
    }

    public async Task ExportCsvToPathAsync(string path)
    {
        if (Manifest is null) return;
        await ExportArtifactAsync(path, SnapshotExportService.ExportCsv(Manifest)).ConfigureAwait(true);
    }

    private async Task FreezeAsync()
    {
        if (IsBusy) return;
        if (FreezeAction is null)
        {
            SetStatus("Freeze is not wired. The shell must provide FreezeAction (SnapshotFreezeService).", isError: true);
            return;
        }

        var snapshotId = (SnapshotIdInput ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            SetStatus("Snapshot ID is required before freezing.", isError: true);
            return;
        }

        try
        {
            IsBusy = true;
            SetStatus("Freezing snapshot...");
            var label = string.IsNullOrWhiteSpace(LabelInput) ? null : LabelInput.Trim();
            var manifest = await FreezeAction(snapshotId, label, CancellationToken.None).ConfigureAwait(true);
            SetManifest(manifest, $"Frozen snapshot '{manifest.SnapshotId}' at revision {manifest.ProjectRevision}. Unknown values remain unknown (not zero).");
        }
        catch (Exception ex)
        {
            SetStatus("Freeze failed: " + ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportJsonAsync()
    {
        if (Manifest is null || IsBusy) return;
        var path = ResolveExportPath("json", "JSON files (*.json)|*.json|All files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(path)) return;
        await ExportJsonToPathAsync(path).ConfigureAwait(true);
    }

    private async Task ExportCsvAsync()
    {
        if (Manifest is null || IsBusy) return;
        var path = ResolveExportPath("csv", "CSV files (*.csv)|*.csv|All files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(path)) return;
        await ExportCsvToPathAsync(path).ConfigureAwait(true);
    }

    private void BrowseExport(string format)
    {
        if (Manifest is null) return;
        var ext = format == "csv" ? "csv" : "json";
        var filter = ext == "csv" ? "CSV files (*.csv)|*.csv|All files (*.*)|*.*" : "JSON files (*.json)|*.json|All files (*.*)|*.*";
        var suggested = $"snapshot-{SanitizeFileName(Manifest.SnapshotId)}.{ext}";
        var picked = PickSaveFile?.Invoke(suggested, filter, $"Export frozen snapshot as {ext.ToUpperInvariant()}");
        if (!string.IsNullOrWhiteSpace(picked))
        {
            ExportPath = picked;
            SetStatus($"Export path selected: {picked}");
        }
    }

    private string? ResolveExportPath(string ext, string filter)
    {
        var path = (ExportPath ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(path))
        {
            // If user gave a directory or path without extension, make it concrete.
            if (Directory.Exists(path))
            {
                path = Path.Combine(path, $"snapshot-{SanitizeFileName(Manifest!.SnapshotId)}.{ext}");
            }
            else
            {
                var currentExt = Path.GetExtension(path);
                if (string.IsNullOrWhiteSpace(currentExt))
                    path = path + "." + ext;
                else if (!string.Equals(currentExt.TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase))
                    path = Path.ChangeExtension(path, ext);
            }

            return path;
        }

        var suggested = $"snapshot-{SanitizeFileName(Manifest!.SnapshotId)}.{ext}";
        var picked = PickSaveFile?.Invoke(suggested, filter, $"Export frozen snapshot as {ext.ToUpperInvariant()}");
        return string.IsNullOrWhiteSpace(picked) ? null : picked;
    }

    private async Task ExportArtifactAsync(string path, SnapshotExportArtifact artifact)
    {
        try
        {
            IsBusy = true;
            var fullPath = Path.GetFullPath(path);
            if (SaveExportAction is not null)
            {
                await SaveExportAction(Manifest!, artifact, fullPath, CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(fullPath, artifact.Content, CancellationToken.None).ConfigureAwait(true);
            }

            ExportPath = fullPath;
            SetStatus($"Export succeeded: {artifact.ContentType} written to '{fullPath}'. Unknown values were preserved as unknown (not zero).");
        }
        catch (Exception ex)
        {
            SetStatus("Export failed: " + ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshCollections()
    {
        SourceRefs.Clear();
        Issues.Clear();
        Exclusions.Clear();
        KnownQuantityLines.Clear();
        ProvisionalQuantityLines.Clear();
        UnknownQuantityLines.Clear();

        if (Manifest is null)
        {
            HasManifestIssues = false;
            HasManifestExclusions = false;
            return;
        }

        foreach (var s in Manifest.SourceRefs) SourceRefs.Add(s);
        foreach (var i in Manifest.Issues) Issues.Add(i);
        foreach (var e in Manifest.Exclusions) Exclusions.Add(e);
        foreach (var q in Manifest.KnownQuantities) KnownQuantityLines.Add(SnapshotQuantityLineDisplay.From(q));
        foreach (var q in Manifest.ProvisionalQuantities) ProvisionalQuantityLines.Add(SnapshotQuantityLineDisplay.From(q));
        foreach (var q in Manifest.UnknownQuantities) UnknownQuantityLines.Add(SnapshotQuantityLineDisplay.From(q));

        HasManifestIssues = Manifest.Issues.Count > 0;
        HasManifestExclusions = Manifest.Exclusions.Count > 0;
    }

    private static string FormatTotals(IReadOnlyList<SnapshotQuantityLine>? lines, string status)
    {
        if (lines is null) return "(none)";
        if (lines.Count == 0) return "(none)";

        if (string.Equals(status, SnapshotQuantityStatus.Unknown, StringComparison.Ordinal))
        {
            // Never present unknown totals as zero. Show explicit unknown label and counts.
            var groups = lines
                .GroupBy(l => $"{l.Ledger} [{l.Unit}]", StringComparer.Ordinal)
                .Select(g => $"{g.Key}: UNKNOWN ({g.Count()} line(s), value not zero)");
            return string.Join("; ", groups);
        }

        var totals = lines
            .GroupBy(l => $"{l.Ledger} [{l.Unit}]", StringComparer.Ordinal)
            .Select(g =>
            {
                var sum = g.Where(x => x.Value.HasValue).Sum(x => x.Value!.Value);
                return $"{g.Key}: {sum.ToString("G17", CultureInfo.InvariantCulture)} ({g.Count()} line(s))";
            });
        return string.Join("; ", totals);
    }

    private static string SanitizeFileName(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        var s = new string(chars);
        return string.IsNullOrWhiteSpace(s) ? "snapshot" : s;
    }

    private void OnCommandError(Exception ex)
    {
        SetStatus("Command failed: " + ex.Message, isError: true);
        IsBusy = false;
    }


}

public sealed record SnapshotQuantityLineDisplay(
    string Ledger,
    string GroupKey,
    string Unit,
    string ValueDisplay,
    string Status,
    string Reason,
    string MethodVersion,
    string SourceRefs,
    string IssueRefs)
{
    public static SnapshotQuantityLineDisplay From(SnapshotQuantityLine line)
    {
        var valueDisplay = line.Status == SnapshotQuantityStatus.Unknown
            ? "UNKNOWN (not zero)"
            : line.Value?.ToString("G17", CultureInfo.InvariantCulture) ?? "UNKNOWN (not zero)";

        return new SnapshotQuantityLineDisplay(
            line.Ledger,
            line.GroupKey,
            line.Unit,
            valueDisplay,
            line.Status,
            line.Reason ?? string.Empty,
            line.MethodVersion ?? string.Empty,
            line.SourceRefs.Count == 0 ? string.Empty : string.Join(", ", line.SourceRefs),
            line.IssueRefs.Count == 0 ? string.Empty : string.Join(", ", line.IssueRefs));
    }
}
