using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OneClick.Application.Agents;
using OneClick.Application.Agents.Audit;
using OneClick.Application.Contracts;
using OneClick.Core.Ids;

namespace OneClick.Infrastructure.Persistence;

/// <summary>
/// Append-only JSONL file-backed store for local agent run audit records.
/// Intended for durable local/offline audit persistence without external DB/network dependencies.
/// </summary>
public sealed class FileAgentRunRecordStore : IAgentRunRecordStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    private readonly string _filePath;
    private readonly object _gate = new();

    public FileAgentRunRecordStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        _filePath = Path.GetFullPath(filePath);
    }

    public string FilePath => _filePath;

    public Task AppendAsync(AgentRunRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(record);

        var line = JsonSerializer.Serialize(AgentRunRecordDto.FromRecord(record), JsonOptions);

        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.WriteLine(line);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AgentRunRecord>> GetByProjectAsync(ProjectId projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(projectId.Value))
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        var records = ReadAll(cancellationToken);
        IReadOnlyList<AgentRunRecord> matches = records
            .Where(r => string.Equals(r.ProjectId.Value, projectId.Value, StringComparison.Ordinal))
            .ToArray();
        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<AgentRunRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("CorrelationId is required.", nameof(correlationId));

        var records = ReadAll(cancellationToken);
        IReadOnlyList<AgentRunRecord> matches = records
            .Where(r => string.Equals(r.CorrelationId, correlationId, StringComparison.Ordinal))
            .ToArray();
        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<AgentRunRecord>> GetRecentAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "maxCount must be positive.");

        var records = ReadAll(cancellationToken);
        IReadOnlyList<AgentRunRecord> matches = records
            .AsEnumerable()
            .Reverse()
            .Take(maxCount)
            .ToArray();
        return Task.FromResult(matches);
    }

    private List<AgentRunRecord> ReadAll(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!File.Exists(_filePath))
                return [];

            string[] lines;
            using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                var content = reader.ReadToEnd();
                lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            }

            var records = new List<AgentRunRecord>(lines.Length);
            for (var i = 0; i < lines.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var lineNumber = i + 1;
                AgentRunRecordDto? dto;
                try
                {
                    dto = JsonSerializer.Deserialize<AgentRunRecordDto>(line, JsonOptions);
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException(
                        $"Agent run record store contains corrupt JSON at '{_filePath}' line {lineNumber}.",
                        ex);
                }

                if (dto is null)
                {
                    throw new InvalidDataException(
                        $"Agent run record store contains a null record at '{_filePath}' line {lineNumber}.");
                }

                try
                {
                    records.Add(dto.ToRecord());
                }
                catch (Exception ex) when (ex is ArgumentException or ArgumentNullException or FormatException or InvalidOperationException)
                {
                    throw new InvalidDataException(
                        $"Agent run record store contains an invalid record at '{_filePath}' line {lineNumber}: {ex.Message}",
                        ex);
                }
            }

            return records;
        }
    }

    private sealed record AgentRunRecordDto(
        string? RunId,
        string? AgentId,
        string? AgentVersion,
        AgentKind AgentKind,
        string? CorrelationId,
        string? ProjectId,
        long ProjectRevision,
        ActorContextDto? Actor,
        string? InputFingerprint,
        string? InputSummary,
        DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt,
        AgentRunStatus Status,
        IReadOnlyList<string>? Issues,
        IReadOnlyList<string>? Warnings,
        IReadOnlyList<string>? ProvenanceRefs,
        IReadOnlyList<string>? OutputRefs,
        string? OutputSummary,
        string? ErrorCode,
        string? ErrorDetail)
    {
        public static AgentRunRecordDto FromRecord(AgentRunRecord record) =>
            new(
                RunId: record.RunId.Value,
                AgentId: record.AgentId.Value,
                AgentVersion: record.AgentVersion,
                AgentKind: record.AgentKind,
                CorrelationId: record.CorrelationId,
                ProjectId: record.ProjectId.Value,
                ProjectRevision: record.ProjectRevision.Value,
                Actor: ActorContextDto.FromActor(record.Actor),
                InputFingerprint: record.InputFingerprint,
                InputSummary: record.InputSummary,
                StartedAt: record.StartedAt,
                EndedAt: record.EndedAt,
                Status: record.Status,
                Issues: record.Issues,
                Warnings: record.Warnings,
                ProvenanceRefs: record.ProvenanceRefs,
                OutputRefs: record.OutputRefs,
                OutputSummary: record.OutputSummary,
                ErrorCode: record.ErrorCode,
                ErrorDetail: record.ErrorDetail);

        public AgentRunRecord ToRecord()
        {
            if (string.IsNullOrWhiteSpace(RunId))
                throw new ArgumentException("RunId is required.");
            if (string.IsNullOrWhiteSpace(AgentId))
                throw new ArgumentException("AgentId is required.");
            if (string.IsNullOrWhiteSpace(AgentVersion))
                throw new ArgumentException("AgentVersion is required.");
            if (string.IsNullOrWhiteSpace(CorrelationId))
                throw new ArgumentException("CorrelationId is required.");
            if (string.IsNullOrWhiteSpace(ProjectId))
                throw new ArgumentException("ProjectId is required.");
            if (Actor is null)
                throw new ArgumentException("Actor is required.");

            return new AgentRunRecord(
                RunId: new AgentRunId(RunId),
                AgentId: new AgentId(AgentId),
                AgentVersion: AgentVersion,
                AgentKind: AgentKind,
                CorrelationId: CorrelationId,
                ProjectId: new ProjectId(ProjectId),
                ProjectRevision: new ProjectRevision(ProjectRevision),
                Actor: Actor.ToActor(),
                InputFingerprint: InputFingerprint,
                InputSummary: InputSummary,
                StartedAt: StartedAt,
                EndedAt: EndedAt,
                Status: Status,
                Issues: Issues ?? [],
                Warnings: Warnings ?? [],
                ProvenanceRefs: ProvenanceRefs ?? [],
                OutputRefs: OutputRefs ?? [],
                OutputSummary: OutputSummary,
                ErrorCode: ErrorCode,
                ErrorDetail: ErrorDetail);
        }
    }

    private sealed record ActorContextDto(string? ActorId, string? SessionId, string? DisplayName)
    {
        public static ActorContextDto FromActor(ActorContext actor) =>
            new(actor.ActorId, actor.SessionId, actor.DisplayName);

        public ActorContext ToActor()
        {
            if (string.IsNullOrWhiteSpace(ActorId))
                throw new ArgumentException("Actor.ActorId is required.");
            if (string.IsNullOrWhiteSpace(SessionId))
                throw new ArgumentException("Actor.SessionId is required.");

            return new ActorContext(ActorId, SessionId, DisplayName);
        }
    }
}
