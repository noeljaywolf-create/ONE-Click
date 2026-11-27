using System.Globalization;
using OneClick.Desktop.Services;

namespace OneClick.Desktop.ViewModels;

/// <summary>
/// UI-facing row for local agent run history/audit display.
/// Values are factual metadata from <see cref="AgentRunRecord"/>; no engineering outcomes are fabricated.
/// </summary>
public sealed record AgentRunHistoryRow(
    string AgentId,
    string AgentName,
    string StartedAt,
    string FinishedAt,
    string Duration,
    string Outcome,
    string ErrorCode,
    int IssueCount,
    int WarningCount,
    int ProvenanceRefCount,
    string CorrelationId)
{
    public static AgentRunHistoryRow FromRecord(AgentRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var duration = record.Duration;
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        return new AgentRunHistoryRow(
            record.AgentId,
            record.AgentName,
            FormatTimestamp(record.StartedAt),
            FormatTimestamp(record.FinishedAt),
            FormatDuration(duration),
            record.IsSuccess ? "Success" : "Error",
            string.IsNullOrWhiteSpace(record.ErrorCode) ? "(none)" : record.ErrorCode,
            record.IssueCount,
            record.WarningCount,
            record.ProvenanceRefCount,
            record.CorrelationId);
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
            return $"{Math.Max(0, (int)duration.TotalMilliseconds)} ms";

        return duration.TotalMinutes < 1
            ? duration.ToString(@"s\.fff\ s", CultureInfo.InvariantCulture)
            : duration.ToString(@"m\:ss\.fff", CultureInfo.InvariantCulture);
    }
}
