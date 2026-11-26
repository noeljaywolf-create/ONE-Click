using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OneClick.Core.Domain;

namespace OneClick.Application.Services;

public sealed record SnapshotExportArtifact(string FileName, string ContentType, string Content);

/// <summary>
/// Deterministic export artifact generation from a frozen snapshot manifest.
/// Known/provisional/unknown quantities remain separated; unknown values are exported as null/empty-value markers, never zero.
/// </summary>
public static class SnapshotExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static SnapshotExportArtifact ExportJson(SnapshotManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var dto = SnapshotExportDto.FromManifest(manifest);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var fileName = $"snapshot-{Sanitize(manifest.SnapshotId)}.json";
        return new SnapshotExportArtifact(fileName, "application/json", json);
    }

    public static SnapshotExportArtifact ExportCsv(SnapshotManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var sb = new StringBuilder();
        sb.AppendLine("# snapshotId,projectId,projectRevision,createdAtUtc,methodVersion");
        sb.AppendLine(string.Join(",",
            Csv(manifest.SnapshotId),
            Csv(manifest.ProjectId),
            Csv(manifest.ProjectRevision.ToString(CultureInfo.InvariantCulture)),
            Csv(manifest.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            Csv(manifest.MethodVersion ?? string.Empty)));

        sb.AppendLine("ledger,groupKey,unit,status,value,reason,methodVersion,issueRefs,sourceRefs");
        foreach (var line in manifest.KnownQuantities.Concat(manifest.ProvisionalQuantities).Concat(manifest.UnknownQuantities))
        {
            var value = line.Value.HasValue
                ? line.Value.Value.ToString("G17", CultureInfo.InvariantCulture)
                : string.Empty; // unknown: empty, never zero

            sb.AppendLine(string.Join(",",
                Csv(line.Ledger),
                Csv(line.GroupKey),
                Csv(line.Unit),
                Csv(line.Status),
                Csv(value),
                Csv(line.Reason ?? string.Empty),
                Csv(line.MethodVersion ?? string.Empty),
                Csv(string.Join("|", line.IssueRefs)),
                Csv(string.Join("|", line.SourceRefs))));
        }

        if (manifest.Exclusions.Count > 0 || manifest.Issues.Count > 0)
        {
            sb.AppendLine("# exclusions");
            foreach (var e in manifest.Exclusions)
                sb.AppendLine(string.Join(",", "exclusion", Csv(e), "", "", "", "", "", "", ""));
            sb.AppendLine("# issues");
            foreach (var i in manifest.Issues)
                sb.AppendLine(string.Join(",", "issue", Csv(i), "", "", "", "", "", "", ""));
        }

        var fileName = $"snapshot-{Sanitize(manifest.SnapshotId)}.csv";
        return new SnapshotExportArtifact(fileName, "text/csv", sb.ToString());
    }

    private static string Sanitize(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        return new string(chars);
    }

    private static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private sealed record SnapshotExportDto(
        string SnapshotId,
        string ProjectId,
        long ProjectRevision,
        DateTimeOffset CreatedAtUtc,
        string? Label,
        IReadOnlyList<string> SourceRefs,
        string? MethodVersion,
        IReadOnlyList<SnapshotQuantityLineDto> KnownQuantities,
        IReadOnlyList<SnapshotQuantityLineDto> ProvisionalQuantities,
        IReadOnlyList<SnapshotQuantityLineDto> UnknownQuantities,
        IReadOnlyList<string> Exclusions,
        IReadOnlyList<string> Issues)
    {
        public static SnapshotExportDto FromManifest(SnapshotManifest manifest) =>
            new(
                manifest.SnapshotId,
                manifest.ProjectId,
                manifest.ProjectRevision,
                manifest.CreatedAtUtc,
                manifest.Label,
                manifest.SourceRefs,
                manifest.MethodVersion,
                manifest.KnownQuantities.Select(SnapshotQuantityLineDto.From).ToArray(),
                manifest.ProvisionalQuantities.Select(SnapshotQuantityLineDto.From).ToArray(),
                manifest.UnknownQuantities.Select(SnapshotQuantityLineDto.From).ToArray(),
                manifest.Exclusions,
                manifest.Issues);
    }

    private sealed record SnapshotQuantityLineDto(
        string Ledger,
        string GroupKey,
        string Unit,
        double? Value,
        string Status,
        string? Reason,
        IReadOnlyList<string> SourceRefs,
        string? MethodVersion,
        IReadOnlyList<string> IssueRefs)
    {
        public static SnapshotQuantityLineDto From(SnapshotQuantityLine line) =>
            new(line.Ledger, line.GroupKey, line.Unit, line.Value, line.Status, line.Reason, line.SourceRefs, line.MethodVersion, line.IssueRefs);
    }
}
