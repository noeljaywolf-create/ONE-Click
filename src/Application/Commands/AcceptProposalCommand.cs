namespace OneClick.Application.Commands;

/// <summary>
/// Contract for accepting a previously generated proposal. Acceptance applies through the application boundary
/// and must not silently mutate reviewed engineering state outside the command transaction.
/// </summary>
public sealed record AcceptProposalCommand(
    string ProposalId,
    string? AcceptanceNotes) : ProjectCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public override string CommandType => CommandTypes.AcceptProposal;
}
