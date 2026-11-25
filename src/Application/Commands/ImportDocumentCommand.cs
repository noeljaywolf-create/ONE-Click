namespace OneClick.Application.Commands;

/// <summary>
/// Contract for importing a staged document into a project. Import processing must create region identities before calibration.
/// </summary>
public sealed record ImportDocumentCommand(
    string UploadHandle,
    string? DocumentLabel,
    string? SourceFileName) : ProjectCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public override string CommandType => CommandTypes.ImportDocument;
}
