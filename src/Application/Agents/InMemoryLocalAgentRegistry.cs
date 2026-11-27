using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using OneClick.Application.Agents.Audit;
using OneClick.Application.Contracts;

namespace OneClick.Application.Agents;

/// <summary>
/// Simple in-memory registry/runner for local agents. Intended for local/offline execution and
/// deterministic composition in tests and first-slice workflows.
/// </summary>
public sealed class InMemoryLocalAgentRegistry : ILocalAgentRegistry
{
    private readonly ConcurrentDictionary<string, ILocalAgent> _agents = new(StringComparer.Ordinal);
    private readonly IAgentRunRecordStore? _runRecordStore;
    private readonly TimeProvider _timeProvider;

    public InMemoryLocalAgentRegistry(IAgentRunRecordStore? runRecordStore = null, TimeProvider? timeProvider = null)
    {
        _runRecordStore = runRecordStore;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void Register(ILocalAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(agent.Descriptor);

        var id = agent.Descriptor.Id.Value;
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Agent descriptor id is required.", nameof(agent));

        if (!_agents.TryAdd(id, agent))
            throw new InvalidOperationException($"Local agent '{id}' is already registered.");
    }

    public bool TryGet(AgentId agentId, out ILocalAgent? agent)
    {
        if (string.IsNullOrWhiteSpace(agentId.Value))
        {
            agent = null;
            return false;
        }

        return _agents.TryGetValue(agentId.Value, out agent);
    }

    public IReadOnlyList<AgentDescriptor> GetDescriptors()
    {
        return _agents.Values
            .Select(a => a.Descriptor)
            .OrderBy(d => d.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<AgentResult<object?>> ExecuteAsync(
        AgentId agentId,
        AgentRequest<object?> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(agentId.Value))
        {
            return AgentResult<object?>.Failure(
                "agent.id.required",
                "Agent id is required.");
        }

        if (!_agents.TryGetValue(agentId.Value, out var agent))
        {
            return AgentResult<object?>.Failure(
                "agent.not_found",
                $"Local agent '{agentId.Value}' is not registered.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = _timeProvider.GetUtcNow();
        try
        {
            var result = await agent.ExecuteAsync(request, context, cancellationToken).ConfigureAwait(false);
            await RecordAsync(agent.Descriptor, context, request.Input, startedAt, result, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException)
        {
            await RecordCancellationAsync(agent.Descriptor, context, request.Input, startedAt, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await RecordExceptionAsync(agent.Descriptor, context, request.Input, startedAt, ex, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<AgentResult<TOutput>> ExecuteAsync<TInput, TOutput>(
        AgentId agentId,
        AgentRequest<TInput> request,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(agentId.Value))
        {
            return AgentResult<TOutput>.Failure(
                "agent.id.required",
                "Agent id is required.");
        }

        if (!_agents.TryGetValue(agentId.Value, out var agent))
        {
            return AgentResult<TOutput>.Failure(
                "agent.not_found",
                $"Local agent '{agentId.Value}' is not registered.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = _timeProvider.GetUtcNow();

        try
        {
            if (agent is ILocalAgent<TInput, TOutput> typedAgent)
            {
                var typedResult = await typedAgent.ExecuteAsync(request, context, cancellationToken).ConfigureAwait(false);
                await RecordAsync(agent.Descriptor, context, request.Input, startedAt, typedResult, cancellationToken).ConfigureAwait(false);
                return typedResult;
            }

            var untyped = await agent.ExecuteAsync(new AgentRequest<object?>(request.Input), context, cancellationToken).ConfigureAwait(false);
            var mapped = MapUntypedResult<TOutput>(agentId, untyped);
            await RecordAsync(agent.Descriptor, context, request.Input, startedAt, mapped, cancellationToken).ConfigureAwait(false);
            return mapped;
        }
        catch (OperationCanceledException)
        {
            await RecordCancellationAsync(agent.Descriptor, context, request.Input, startedAt, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await RecordExceptionAsync(agent.Descriptor, context, request.Input, startedAt, ex, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task RecordAsync<TOutput>(
        AgentDescriptor descriptor,
        AgentRunContext context,
        object? input,
        DateTimeOffset startedAt,
        AgentResult<TOutput> result,
        CancellationToken cancellationToken)
    {
        if (_runRecordStore is null)
            return;

        var endedAt = _timeProvider.GetUtcNow();
        var record = new AgentRunRecord(
            RunId: AgentRunId.NewId(),
            AgentId: descriptor.Id,
            AgentVersion: descriptor.Version,
            AgentKind: descriptor.Kind,
            CorrelationId: context.CorrelationId,
            ProjectId: context.ProjectId,
            ProjectRevision: context.ProjectRevision,
            Actor: context.Actor,
            InputFingerprint: ComputeInputFingerprint(input),
            InputSummary: ComputeInputSummary(input),
            StartedAt: startedAt,
            EndedAt: endedAt,
            Status: result.IsSuccess ? AgentRunStatus.Succeeded : AgentRunStatus.Failed,
            Issues: result.Issues,
            Warnings: result.Warnings,
            ProvenanceRefs: result.ProvenanceRefs,
            OutputRefs: Array.Empty<string>(),
            OutputSummary: ComputeOutputSummary(result),
            ErrorCode: result.ErrorCode,
            ErrorDetail: result.ErrorDetail);

        await _runRecordStore.AppendAsync(record, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task RecordCancellationAsync(
        AgentDescriptor descriptor,
        AgentRunContext context,
        object? input,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        if (_runRecordStore is null)
            return;

        var endedAt = _timeProvider.GetUtcNow();
        var record = new AgentRunRecord(
            RunId: AgentRunId.NewId(),
            AgentId: descriptor.Id,
            AgentVersion: descriptor.Version,
            AgentKind: descriptor.Kind,
            CorrelationId: context.CorrelationId,
            ProjectId: context.ProjectId,
            ProjectRevision: context.ProjectRevision,
            Actor: context.Actor,
            InputFingerprint: ComputeInputFingerprint(input),
            InputSummary: ComputeInputSummary(input),
            StartedAt: startedAt,
            EndedAt: endedAt,
            Status: AgentRunStatus.Cancelled,
            Issues: Array.Empty<string>(),
            Warnings: Array.Empty<string>(),
            ProvenanceRefs: Array.Empty<string>(),
            OutputRefs: Array.Empty<string>(),
            OutputSummary: null,
            ErrorCode: "agent.execution.cancelled",
            ErrorDetail: $"Local agent '{descriptor.Id}' execution was cancelled.");

        // Record with CancellationToken.None to avoid losing audit on cancellation.
        await _runRecordStore.AppendAsync(record, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task RecordExceptionAsync(
        AgentDescriptor descriptor,
        AgentRunContext context,
        object? input,
        DateTimeOffset startedAt,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (_runRecordStore is null)
            return;

        var endedAt = _timeProvider.GetUtcNow();
        var record = new AgentRunRecord(
            RunId: AgentRunId.NewId(),
            AgentId: descriptor.Id,
            AgentVersion: descriptor.Version,
            AgentKind: descriptor.Kind,
            CorrelationId: context.CorrelationId,
            ProjectId: context.ProjectId,
            ProjectRevision: context.ProjectRevision,
            Actor: context.Actor,
            InputFingerprint: ComputeInputFingerprint(input),
            InputSummary: ComputeInputSummary(input),
            StartedAt: startedAt,
            EndedAt: endedAt,
            Status: AgentRunStatus.Failed,
            Issues: Array.Empty<string>(),
            Warnings: Array.Empty<string>(),
            ProvenanceRefs: Array.Empty<string>(),
            OutputRefs: Array.Empty<string>(),
            OutputSummary: null,
            ErrorCode: "agent.execution.exception",
            ErrorDetail: exception.GetType().FullName ?? exception.GetType().Name);

        await _runRecordStore.AppendAsync(record, CancellationToken.None).ConfigureAwait(false);
    }

    private static string ComputeInputSummary(object? input)
    {
        var typeName = input?.GetType().FullName ?? "null";
        return $"type={typeName}";
    }

    private static string ComputeInputFingerprint(object? input)
    {
        var summary = ComputeInputSummary(input);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(summary));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ComputeOutputSummary<TOutput>(AgentResult<TOutput> result)
    {
        var outputType = result.Output?.GetType().FullName ?? typeof(TOutput).FullName ?? "unknown";
        return $"type={outputType};success={result.IsSuccess}";
    }


    private static AgentResult<TOutput> MapUntypedResult<TOutput>(AgentId agentId, AgentResult<object?> result)
    {
        if (!result.IsSuccess)
        {
            return AgentResult<TOutput>.Failure(
                result.ErrorCode ?? "agent.execution.failed",
                result.ErrorDetail ?? $"Local agent '{agentId.Value}' execution failed.",
                result.Issues,
                result.Warnings,
                result.ProvenanceRefs);
        }

        if (result.Output is TOutput typedOutput)
        {
            return AgentResult<TOutput>.Success(typedOutput, result.Issues, result.Warnings, result.ProvenanceRefs);
        }

        if (result.Output is null && default(TOutput) is null)
        {
            return AgentResult<TOutput>.Success(default!, result.Issues, result.Warnings, result.ProvenanceRefs);
        }

        return AgentResult<TOutput>.Failure(
            "agent.output.invalid_type",
            $"Local agent '{agentId.Value}' returned output type '{result.Output?.GetType().FullName ?? "null"}' but '{typeof(TOutput).FullName}' was requested.",
            result.Issues,
            result.Warnings,
            result.ProvenanceRefs);
    }
}
