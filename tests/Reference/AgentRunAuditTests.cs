using OneClick.Application.Agents;
using OneClick.Application.Agents.Audit;
using OneClick.Application.Contracts;
using OneClick.Core.Ids;

namespace OneClick.Tests.Reference;

public sealed class AgentRunAuditTests
{
    [Fact]
    public void AgentRunRecord_NormalizesCollections_AndRequiresIdentity()
    {
        var record = new AgentRunRecord(
            RunId: new AgentRunId("run-1"),
            AgentId: new AgentId("agent.test"),
            AgentVersion: "1.0.0",
            AgentKind: AgentKind.Deterministic,
            CorrelationId: "corr-1",
            ProjectId: new ProjectId("project-1"),
            ProjectRevision: new ProjectRevision(7),
            Actor: new ActorContext("actor-1", "session-1"),
            InputFingerprint: "fingerprint",
            InputSummary: "summary",
            StartedAt: new DateTimeOffset(2026, 11, 27, 0, 0, 0, TimeSpan.Zero),
            EndedAt: new DateTimeOffset(2026, 11, 27, 0, 0, 1, TimeSpan.Zero),
            Status: AgentRunStatus.Succeeded,
            Issues: ["issue:a"],
            Warnings: ["warn:a"],
            ProvenanceRefs: ["prov:a"],
            OutputRefs: ["out:a"],
            OutputSummary: "output",
            ErrorCode: null,
            ErrorDetail: null);

        Assert.Equal("run-1", record.RunId.Value);
        Assert.Equal("agent.test", record.AgentId.Value);
        Assert.Equal("1.0.0", record.AgentVersion);
        Assert.Equal(AgentKind.Deterministic, record.AgentKind);
        Assert.Equal("corr-1", record.CorrelationId);
        Assert.Equal("project-1", record.ProjectId.Value);
        Assert.Equal(7, record.ProjectRevision.Value);
        Assert.Equal("actor-1", record.Actor.ActorId);
        Assert.Equal("session-1", record.Actor.SessionId);
        Assert.Equal("fingerprint", record.InputFingerprint);
        Assert.Equal("summary", record.InputSummary);
        Assert.Equal(AgentRunStatus.Succeeded, record.Status);
        Assert.Single(record.Issues);
        Assert.Single(record.Warnings);
        Assert.Single(record.ProvenanceRefs);
        Assert.Single(record.OutputRefs);
        Assert.Equal("output", record.OutputSummary);
    }

    [Fact]
    public void AgentRunRecord_RejectsMissingRequiredIdentity()
    {
        Assert.Throws<ArgumentException>(() => new AgentRunRecord(
            RunId: new AgentRunId(" "),
            AgentId: new AgentId("agent.test"),
            AgentVersion: "1.0.0",
            AgentKind: AgentKind.Deterministic,
            CorrelationId: "corr-1",
            ProjectId: new ProjectId("project-1"),
            ProjectRevision: new ProjectRevision(0),
            Actor: new ActorContext("actor-1", "session-1"),
            InputFingerprint: null,
            InputSummary: null,
            StartedAt: DateTimeOffset.UtcNow,
            EndedAt: null,
            Status: AgentRunStatus.Running,
            Issues: [],
            Warnings: [],
            ProvenanceRefs: [],
            OutputRefs: [],
            OutputSummary: null));

        Assert.Throws<ArgumentException>(() => new AgentRunRecord(
            RunId: new AgentRunId("run-1"),
            AgentId: new AgentId("agent.test"),
            AgentVersion: " ",
            AgentKind: AgentKind.Deterministic,
            CorrelationId: "corr-1",
            ProjectId: new ProjectId("project-1"),
            ProjectRevision: new ProjectRevision(0),
            Actor: new ActorContext("actor-1", "session-1"),
            InputFingerprint: null,
            InputSummary: null,
            StartedAt: DateTimeOffset.UtcNow,
            EndedAt: null,
            Status: AgentRunStatus.Running,
            Issues: [],
            Warnings: [],
            ProvenanceRefs: [],
            OutputRefs: [],
            OutputSummary: null));
    }

    [Fact]
    public async Task InMemoryAgentRunRecordStore_AppendsAndQueriesByProjectAndCorrelation()
    {
        var store = new InMemoryAgentRunRecordStore();
        var projectA = new ProjectId("project-a");
        var projectB = new ProjectId("project-b");

        var recordA1 = CreateRecord("run-a1", "agent.one", projectA, "corr-1", AgentRunStatus.Succeeded);
        var recordA2 = CreateRecord("run-a2", "agent.two", projectA, "corr-2", AgentRunStatus.Failed);
        var recordB1 = CreateRecord("run-b1", "agent.one", projectB, "corr-1", AgentRunStatus.Succeeded);

        await store.AppendAsync(recordA1);
        await store.AppendAsync(recordA2);
        await store.AppendAsync(recordB1);

        var byProjectA = await store.GetByProjectAsync(projectA);
        var byCorrelation1 = await store.GetByCorrelationIdAsync("corr-1");

        Assert.Equal(2, byProjectA.Count);
        Assert.Equal(["run-a1", "run-a2"], byProjectA.Select(r => r.RunId.Value).ToArray());

        Assert.Equal(2, byCorrelation1.Count);
        Assert.Equal(["run-a1", "run-b1"], byCorrelation1.Select(r => r.RunId.Value).ToArray());
    }

    [Fact]
    public async Task InMemoryAgentRunRecordStore_GetRecent_ReturnsMostRecentlyAppendedFirst()
    {
        var store = new InMemoryAgentRunRecordStore();
        var project = new ProjectId("project-a");

        await store.AppendAsync(CreateRecord("run-1", "agent.one", project, "corr-1", AgentRunStatus.Succeeded));
        await store.AppendAsync(CreateRecord("run-2", "agent.one", project, "corr-2", AgentRunStatus.Succeeded));
        await store.AppendAsync(CreateRecord("run-3", "agent.one", project, "corr-3", AgentRunStatus.Failed));

        var recent = await store.GetRecentAsync(2);

        Assert.Equal(["run-3", "run-2"], recent.Select(r => r.RunId.Value).ToArray());
    }

    [Fact]
    public async Task Registry_RecordsSuccessfulRun_WithAuditFields()
    {
        var store = new InMemoryAgentRunRecordStore();
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero));
        var registry = new InMemoryLocalAgentRegistry(store, time);
        var descriptor = new AgentDescriptor(new AgentId("agent.test.success"), AgentKind.Deterministic, "Test", "Test agent", "1.2.3");
        registry.Register(new FixedAgent(descriptor, AgentResult<string>.Success(
            "ok",
            issues: ["issue:1"],
            warnings: ["warn:1"],
            provenanceRefs: ["prov:1"])));

        var context = new AgentRunContext(
            new ProjectId("project-1"),
            new ActorContext("actor-1", "session-1"),
            "corr-1",
            CancellationToken.None,
            new ProjectRevision(42));

        var result = await registry.ExecuteAsync<string, string>(
            descriptor.Id,
            AgentRequest<string>.From("input"),
            context);

        Assert.True(result.IsSuccess);
        var records = await store.GetByProjectAsync(new ProjectId("project-1"));
        var record = Assert.Single(records);
        Assert.Equal(descriptor.Id, record.AgentId);
        Assert.Equal("1.2.3", record.AgentVersion);
        Assert.Equal(AgentKind.Deterministic, record.AgentKind);
        Assert.Equal("corr-1", record.CorrelationId);
        Assert.Equal(42, record.ProjectRevision.Value);
        Assert.Equal("actor-1", record.Actor.ActorId);
        Assert.Equal("session-1", record.Actor.SessionId);
        Assert.Equal(AgentRunStatus.Succeeded, record.Status);
        Assert.Equal(["issue:1"], record.Issues);
        Assert.Equal(["warn:1"], record.Warnings);
        Assert.Equal(["prov:1"], record.ProvenanceRefs);
        Assert.False(string.IsNullOrWhiteSpace(record.InputFingerprint));
        Assert.Contains("System.String", record.InputSummary);
        Assert.Contains("success=True", record.OutputSummary);
        Assert.Equal(time.Now, record.StartedAt);
        Assert.Equal(time.Now, record.EndedAt);
        Assert.Null(record.ErrorCode);
    }

    [Fact]
    public async Task Registry_RecordsFailedRun_WithErrorAndRefs()
    {
        var store = new InMemoryAgentRunRecordStore();
        var registry = new InMemoryLocalAgentRegistry(store);
        var descriptor = new AgentDescriptor(new AgentId("agent.test.fail"), AgentKind.Deterministic, "Test", "Test agent", "1.0.0");
        registry.Register(new FixedAgent(descriptor, AgentResult<string>.Failure(
            "agent.failed",
            "failed",
            issues: ["issue:bad"],
            warnings: ["warn:bad"],
            provenanceRefs: ["prov:bad"])));

        var context = new AgentRunContext(new ProjectId("project-1"), new ActorContext("actor-1", "session-1"), "corr-fail", CancellationToken.None);
        var result = await registry.ExecuteAsync<string, string>(descriptor.Id, AgentRequest<string>.From("input"), context);

        Assert.False(result.IsSuccess);
        var record = Assert.Single(await store.GetByCorrelationIdAsync("corr-fail"));
        Assert.Equal(AgentRunStatus.Failed, record.Status);
        Assert.Equal("agent.failed", record.ErrorCode);
        Assert.Equal("failed", record.ErrorDetail);
        Assert.Equal(["issue:bad"], record.Issues);
        Assert.Equal(["warn:bad"], record.Warnings);
        Assert.Equal(["prov:bad"], record.ProvenanceRefs);
    }

    [Fact]
    public async Task Registry_RecordsCancelledRun_WhenAgentThrowsOperationCanceled()
    {
        var store = new InMemoryAgentRunRecordStore();
        var registry = new InMemoryLocalAgentRegistry(store);
        var descriptor = new AgentDescriptor(new AgentId("agent.test.cancel"), AgentKind.Deterministic, "Test", "Test agent", "1.0.0");
        registry.Register(new ThrowingAgent(descriptor, new OperationCanceledException("cancelled")));

        var context = new AgentRunContext(new ProjectId("project-1"), new ActorContext("actor-1", "session-1"), "corr-cancel", CancellationToken.None);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            registry.ExecuteAsync<string, string>(descriptor.Id, AgentRequest<string>.From("input"), context));

        var record = Assert.Single(await store.GetByCorrelationIdAsync("corr-cancel"));
        Assert.Equal(AgentRunStatus.Cancelled, record.Status);
        Assert.Equal("agent.execution.cancelled", record.ErrorCode);
    }

    [Fact]
    public async Task Registry_WithoutStore_DoesNotChangeExecutionBehavior()
    {
        var registry = new InMemoryLocalAgentRegistry();
        var descriptor = new AgentDescriptor(new AgentId("agent.test.nostore"), AgentKind.Deterministic, "Test", "Test agent", "1.0.0");
        registry.Register(new FixedAgent(descriptor, AgentResult<string>.Success("ok")));
        var context = new AgentRunContext(new ProjectId("project-1"), new ActorContext("actor-1", "session-1"), "corr-1", CancellationToken.None);

        var result = await registry.ExecuteAsync<string, string>(descriptor.Id, AgentRequest<string>.From("input"), context);

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Output);
    }

    private static AgentRunRecord CreateRecord(string runId, string agentId, ProjectId projectId, string correlationId, AgentRunStatus status)
    {
        return new AgentRunRecord(
            RunId: new AgentRunId(runId),
            AgentId: new AgentId(agentId),
            AgentVersion: "1.0.0",
            AgentKind: AgentKind.Deterministic,
            CorrelationId: correlationId,
            ProjectId: projectId,
            ProjectRevision: new ProjectRevision(1),
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

    private sealed class FixedAgent : LocalAgentBase<string, string>
    {
        private readonly AgentResult<string> _result;

        public FixedAgent(AgentDescriptor descriptor, AgentResult<string> result)
            : base(descriptor)
        {
            _result = result;
        }

        public override Task<AgentResult<string>> ExecuteAsync(
            AgentRequest<string> request,
            AgentRunContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);
    }

    private sealed class ThrowingAgent : LocalAgentBase<string, string>
    {
        private readonly Exception _exception;

        public ThrowingAgent(AgentDescriptor descriptor, Exception exception)
            : base(descriptor)
        {
            _exception = exception;
        }

        public override Task<AgentResult<string>> ExecuteAsync(
            AgentRequest<string> request,
            AgentRunContext context,
            CancellationToken cancellationToken = default) =>
            throw _exception;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public FixedTimeProvider(DateTimeOffset now) => Now = now;

        public DateTimeOffset Now { get; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
