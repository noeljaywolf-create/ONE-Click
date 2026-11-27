using OneClick.Application.Agents.Audit;
using OneClick.Application.Persistence;
using OneClick.Application.Services;

namespace OneClick.Application.Agents.Takeoff;

/// <summary>
/// Registers deterministic local takeoff agents into a local agent registry.
/// </summary>
public static class TakeoffAgentRegistration
{
    /// <summary>
    /// Registers baseline load, ledger calculation, snapshot freeze, document import metadata,
    /// calibration metadata, and deterministic validation agents.
    /// All agents are local/offline and deterministic; no network/AI calls.
    /// </summary>
    public static ILocalAgentRegistry RegisterTakeoffAgents(
        this ILocalAgentRegistry registry,
        IProjectDomainStateStore domainStateStore)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(domainStateStore);

        return registry.RegisterTakeoffAgents(domainStateStore, new SnapshotFreezeService(domainStateStore));
    }

    /// <summary>
    /// Registers baseline load, ledger calculation, snapshot freeze, document import metadata,
    /// calibration metadata, and deterministic validation agents with an explicit snapshot freeze service.
    /// </summary>
    public static ILocalAgentRegistry RegisterTakeoffAgents(
        this ILocalAgentRegistry registry,
        IProjectDomainStateStore domainStateStore,
        SnapshotFreezeService snapshotFreezeService)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(domainStateStore);
        ArgumentNullException.ThrowIfNull(snapshotFreezeService);

        registry.Register(new BaselineLoadAgent(domainStateStore));
        registry.Register(new LedgerCalculationAgent(domainStateStore));
        registry.Register(new SnapshotFreezeAgent(snapshotFreezeService));
        registry.Register(new ImportDocumentAgent(domainStateStore));
        registry.Register(new SetCalibrationAgent(domainStateStore));
        registry.Register(new ValidationAgent(domainStateStore));
        return registry;
    }

    /// <summary>
    /// Creates a new in-memory registry with deterministic takeoff agents registered.
    /// </summary>
    public static InMemoryLocalAgentRegistry CreateTakeoffRegistry(
        IProjectDomainStateStore domainStateStore,
        IAgentRunRecordStore? runRecordStore = null)
    {
        ArgumentNullException.ThrowIfNull(domainStateStore);

        var registry = new InMemoryLocalAgentRegistry(runRecordStore);
        registry.RegisterTakeoffAgents(domainStateStore);
        return registry;
    }
}
