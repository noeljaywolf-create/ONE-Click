namespace OneClick.Application.Commands;

/// <summary>
/// Contract for selecting an assembly resolution candidate. Selection is applied through the application boundary
/// against pinned catalogue and model context.
/// </summary>
public sealed record SelectAssemblyCommand(
    string AssemblyContextId,
    string SelectedCandidateId,
    string? SelectionNotes) : ProjectCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public override string CommandType => CommandTypes.SelectAssembly;
}
