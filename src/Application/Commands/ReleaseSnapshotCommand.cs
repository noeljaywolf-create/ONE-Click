namespace OneClick.Application.Commands;

/// <summary>
/// Contract for releasing an immutable snapshot. Release checks authorized role, compatible purpose,
/// and unresolved-item policy at the application boundary. Release cannot change draft quantities or mark assumptions as facts.
/// </summary>
public sealed record ReleaseSnapshotCommand(
    string SnapshotId,
    string ReleasePurpose,
    string? ReleaseNotes,
    IReadOnlyList<string>? AcknowledgedIssueIds = null) : ProjectCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public override string CommandType => CommandTypes.ReleaseSnapshot;
}
