using System.Text.Json.Serialization;

namespace OneClick.Application.Commands;

/// <summary>
/// Base type for typed application commands. Commands are validated and applied by the application boundary;
/// UI/AI/worker modules do not write domain tables directly.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "commandType")]
[JsonDerivedType(typeof(ImportDocumentCommand), CommandTypes.ImportDocument)]
[JsonDerivedType(typeof(SetCalibrationCommand), CommandTypes.SetCalibration)]
[JsonDerivedType(typeof(UpsertRouteCommand), CommandTypes.UpsertRoute)]
[JsonDerivedType(typeof(PlaceComponentCommand), CommandTypes.PlaceComponent)]
[JsonDerivedType(typeof(ConnectPortsCommand), CommandTypes.ConnectPorts)]
[JsonDerivedType(typeof(AcceptProposalCommand), CommandTypes.AcceptProposal)]
[JsonDerivedType(typeof(ReviewPropertyCommand), CommandTypes.ReviewProperty)]
[JsonDerivedType(typeof(SelectAssemblyCommand), CommandTypes.SelectAssembly)]
[JsonDerivedType(typeof(ReleaseSnapshotCommand), CommandTypes.ReleaseSnapshot)]
public abstract record ProjectCommand
{
    /// <summary>
    /// Stable command type name for logging, contracts, and diagnostics.
    /// </summary>
    [JsonIgnore]
    public abstract string CommandType { get; }
}
