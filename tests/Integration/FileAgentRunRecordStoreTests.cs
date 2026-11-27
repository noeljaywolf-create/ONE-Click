using OneClick.Application.Agents;
using OneClick.Application.Agents.Audit;
using OneClick.Application.Contracts;
using OneClick.Core.Ids;
using OneClick.Infrastructure.Persistence;

namespace OneClick.Tests.Integration;

public sealed class FileAgentRunRecordStoreTests : IDisposable
{
    private readonly string _tempDir;

    public FileAgentRunRecordStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "OneClickTests", "AgentRunRecordStore", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task AppendAndQuery_ByProjectCorrelationAndRecent_RoundTrips()
    {
        var path = Path.Combine(_tempDir, "runs.jsonl");
        var store = new FileAgentRunRecordStore(path);
        var projectA = new ProjectId("project-a");
        var projectB = new ProjectId("project-b");

        var recordA1 = CreateRecord("run-a1", "agent.one", projectA, "corr-1", AgentRunStatus.Succeeded, new ProjectRevision(7));
        var recordA2 = CreateRecord("run-a2", "agent.two", projectA, "corr-2", AgentRunStatus.Failed, new ProjectRevision(8));
        var recordB1 = CreateRecord("run-b1", "agent.one", projectB, "corr-1", AgentRunStatus.Cancelled, new ProjectRevision(1));

        await store.AppendAsync(recordA1);
        await store.AppendAsync(recordA2);
        await store.AppendAsync(recordB1);

        var byProjectA = await store.GetByProjectAsync(projectA);
        var byCorrelation1 = await store.GetByCorrelationIdAsync("corr-1");
        var recent = await store.GetRecentAsync(2);

        Assert.Equal(["run-a1", "run-a2"], byProjectA.Select(r => r.RunId.Value).ToArray());
        Assert.Equal(["run-a1", "run-b1"], byCorrelation1.Select(r => r.RunId.Value).ToArray());
        Assert.Equal(["run-b1", "run-a2"], recent.Select(r => r.RunId.Value).ToArray());
    }

    [Fact]
    public async Task MissingFile_ReturnsEmptyResults()
    {
        var path = Path.Combine(_tempDir, "missing", "runs.jsonl");
        var store = new FileAgentRunRecordStore(path);

        var byProject = await store.GetByProjectAsync(new ProjectId("project-a"));
        var byCorrelation = await store.GetByCorrelationIdAsync("corr-1");
        var recent = await store.GetRecentAsync(10);

        Assert.Empty(byProject);
        Assert.Empty(byCorrelation);
        Assert.Empty(recent);
    }

    [Fact]
    public async Task Append_CreatesDirectoryAndPersistsAcrossInstances()
    {
        var path = Path.Combine(_tempDir, "nested", "audit", "runs.jsonl");
        var store = new FileAgentRunRecordStore(path);
        var record = CreateRecord("run-1", "agent.one", new ProjectId("project-a"), "corr-1", AgentRunStatus.Succeeded, new ProjectRevision(3));

        await store.AppendAsync(record);

        Assert.True(File.Exists(path));

        var reloaded = new FileAgentRunRecordStore(path);
        var records = await reloaded.GetByProjectAsync(new ProjectId("project-a"));
        var loaded = Assert.Single(records);
        AssertRecordEqual(record, loaded);
    }

    [Fact]
    public async Task CorruptJsonLine_ThrowsInvalidDataException_WithFileAndLine()
    {
        var path = Path.Combine(_tempDir, "runs.jsonl");
        var store = new FileAgentRunRecordStore(path);
        await store.AppendAsync(CreateRecord("run-1", "agent.one", new ProjectId("project-a"), "corr-1", AgentRunStatus.Succeeded, new ProjectRevision(1)));

        File.AppendAllText(path, "{not json}\n");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => store.GetByProjectAsync(new ProjectId("project-a")));
        Assert.Contains(path, ex.Message);
        Assert.Contains("line 2", ex.Message);
    }

    [Fact]
    public async Task InvalidRecordLine_ThrowsInvalidDataException_NotSilentDataLoss()
    {
        var path = Path.Combine(_tempDir, "runs.jsonl");
        var store = new FileAgentRunRecordStore(path);
        await store.AppendAsync(CreateRecord("run-1", "agent.one", new ProjectId("project-a"), "corr-1", AgentRunStatus.Succeeded, new ProjectRevision(1)));

        // Valid JSON but missing required identity fields.
        File.AppendAllText(path, "{\"runId\":\"\",\"agentId\":\"agent.one\"}\n");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => store.GetRecentAsync(10));
        Assert.Contains("line 2", ex.Message);
    }

    [Fact]
    public async Task RoundTrip_PreservesAllAuditFields()
    {
        var path = Path.Combine(_tempDir, "runs.jsonl");
        var store = new FileAgentRunRecordStore(path);
        var record = new AgentRunRecord(
            RunId: new AgentRunId("run-rt"),
            AgentId: new AgentId("agent.test"),
            AgentVersion: "1.2.3",
            AgentKind: AgentKind.Deterministic,
            CorrelationId: "corr-rt",
            ProjectId: new ProjectId("project-rt"),
            ProjectRevision: new ProjectRevision(42),
            Actor: new ActorContext("actor-1", "session-1", "Display Name"),
            InputFingerprint: "fingerprint",
            InputSummary: "type=System.String",
            StartedAt: new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero),
            EndedAt: new DateTimeOffset(2026, 11, 27, 12, 0, 1, TimeSpan.Zero),
            Status: AgentRunStatus.Failed,
            Issues: ["issue:a", "issue:b"],
            Warnings: ["warn:a"],
            ProvenanceRefs: ["prov:a"],
            OutputRefs: ["out:a"],
            OutputSummary: "type=System.String;success=False",
            ErrorCode: "agent.failed",
            ErrorDetail: "detail");

        await store.AppendAsync(record);

        var reloaded = new FileAgentRunRecordStore(path);
        var loaded = Assert.Single(await reloaded.GetByCorrelationIdAsync("corr-rt"));
        AssertRecordEqual(record, loaded);
    }

    [Fact]
    public async Task GetRecent_ValidatesMaxCount()
    {
        var store = new FileAgentRunRecordStore(Path.Combine(_tempDir, "runs.jsonl"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.GetRecentAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.GetRecentAsync(-1));
    }

    private static AgentRunRecord CreateRecord(
        string runId,
        string agentId,
        ProjectId projectId,
        string correlationId,
        AgentRunStatus status,
        ProjectRevision revision)
    {
        return new AgentRunRecord(
            RunId: new AgentRunId(runId),
            AgentId: new AgentId(agentId),
            AgentVersion: "1.0.0",
            AgentKind: AgentKind.Deterministic,
            CorrelationId: correlationId,
            ProjectId: projectId,
            ProjectRevision: revision,
            Actor: new ActorContext("actor-1", "session-1"),
            InputFingerprint: "fp",
            InputSummary: "summary",
            StartedAt: new DateTimeOffset(2026, 11, 27, 0, 0, 0, TimeSpan.Zero),
            EndedAt: new DateTimeOffset(2026, 11, 27, 0, 0, 1, TimeSpan.Zero),
            Status: status,
            Issues: [],
            Warnings: [],
            ProvenanceRefs: [],
            OutputRefs: [],
            OutputSummary: "output");
    }

    private static void AssertRecordEqual(AgentRunRecord expected, AgentRunRecord actual)
    {
        Assert.Equal(expected.RunId, actual.RunId);
        Assert.Equal(expected.AgentId, actual.AgentId);
        Assert.Equal(expected.AgentVersion, actual.AgentVersion);
        Assert.Equal(expected.AgentKind, actual.AgentKind);
        Assert.Equal(expected.CorrelationId, actual.CorrelationId);
        Assert.Equal(expected.ProjectId, actual.ProjectId);
        Assert.Equal(expected.ProjectRevision, actual.ProjectRevision);
        Assert.Equal(expected.Actor, actual.Actor);
        Assert.Equal(expected.InputFingerprint, actual.InputFingerprint);
        Assert.Equal(expected.InputSummary, actual.InputSummary);
        Assert.Equal(expected.StartedAt, actual.StartedAt);
        Assert.Equal(expected.EndedAt, actual.EndedAt);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Issues, actual.Issues);
        Assert.Equal(expected.Warnings, actual.Warnings);
        Assert.Equal(expected.ProvenanceRefs, actual.ProvenanceRefs);
        Assert.Equal(expected.OutputRefs, actual.OutputRefs);
        Assert.Equal(expected.OutputSummary, actual.OutputSummary);
        Assert.Equal(expected.ErrorCode, actual.ErrorCode);
        Assert.Equal(expected.ErrorDetail, actual.ErrorDetail);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }
}
