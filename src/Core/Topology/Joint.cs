using OneClick.Core.Ids;

namespace OneClick.Core.Topology;

/// <summary>
/// Immutable confirmed joint. A joint exists only for confirmed ports; a crossing or nearby
/// endpoint is modeled as a connection candidate until validated.
/// </summary>
public sealed record Joint
{
    public Joint(
        JointId id,
        IReadOnlyList<PortId> confirmedPortIds,
        string selectedJoiningAdapterId,
        AssemblyId assemblyOwnershipId)
    {
        Guard.Required(id.Value, nameof(id));
        Id = id;
        ConfirmedPortIds = NormalizeConfirmedPortIds(confirmedPortIds);
        SelectedJoiningAdapterId = Guard.Required(selectedJoiningAdapterId, nameof(selectedJoiningAdapterId));
        Guard.Required(assemblyOwnershipId.Value, nameof(assemblyOwnershipId));
        AssemblyOwnershipId = assemblyOwnershipId;
    }

    public JointId Id { get; }

    public IReadOnlyList<PortId> ConfirmedPortIds { get; }

    public string SelectedJoiningAdapterId { get; }

    public AssemblyId AssemblyOwnershipId { get; }

    private static IReadOnlyList<PortId> NormalizeConfirmedPortIds(IReadOnlyList<PortId>? portIds)
    {
        ArgumentNullException.ThrowIfNull(portIds, nameof(portIds));
        if (portIds.Count < 2)
            throw new ArgumentException("A joint requires at least two confirmed ports.", nameof(portIds));

        var copy = new PortId[portIds.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < portIds.Count; i++)
        {
            var portId = portIds[i];
            Guard.Required(portId.Value, $"confirmedPortIds[{i}]");
            if (!seen.Add(portId.Value))
                throw new ArgumentException($"Confirmed port '{portId.Value}' is duplicated.", nameof(portIds));
            copy[i] = portId;
        }

        return Array.AsReadOnly(copy);
    }
}
