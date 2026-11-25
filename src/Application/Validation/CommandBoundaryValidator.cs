using OneClick.Application.Commands;
using OneClick.Application.Contracts;
using OneClick.Application.Errors;
using OneClick.Core.Ids;

namespace OneClick.Application.Validation;

/// <summary>
/// Gate 0 boundary validator. Performs structural/contract checks that do not require persistence.
/// Optional context hooks enable later project-scope, reference-existence, and expected-revision checks.
/// </summary>
public sealed class CommandBoundaryValidator : ICommandBoundaryValidator
{
    private static readonly HashSet<string> KnownCommandTypes = new(StringComparer.Ordinal)
    {
        CommandTypes.ImportDocument,
        CommandTypes.SetCalibration,
        CommandTypes.UpsertRoute,
        CommandTypes.PlaceComponent,
        CommandTypes.ConnectPorts,
        CommandTypes.AcceptProposal,
        CommandTypes.ReviewProperty,
        CommandTypes.SelectAssembly,
        CommandTypes.ReleaseSnapshot,
    };

    public async Task<CommandValidationResult> ValidateAsync(
        CommandValidationRequest request,
        ICommandValidationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problems = new List<ApplicationProblemDetails>();

        ValidateProjectId(request.ProjectId, problems);
        ValidateExpectedRevision(request.ExpectedRevision, problems);
        ValidateCommandId(request.CommandId, problems);
        ValidateActorContext(request.ActorContext, problems);
        ValidateCommand(request.Command, problems);

        if (context is not null && problems.Count == 0)
        {
            await ValidateWithContextAsync(request, context, problems, cancellationToken).ConfigureAwait(false);
        }

        return problems.Count == 0
            ? CommandValidationResult.Success()
            : CommandValidationResult.Failure(problems);
    }

    private static void ValidateProjectId(ProjectId projectId, List<ApplicationProblemDetails> problems)
    {
        if (string.IsNullOrWhiteSpace(projectId.Value))
        {
            problems.Add(Malformed("ProjectId is required.", "projectId"));
        }
    }

    private static void ValidateExpectedRevision(ProjectRevision expectedRevision, List<ApplicationProblemDetails> problems)
    {
        if (expectedRevision.Value < 0)
        {
            problems.Add(Malformed("ExpectedRevision must be zero or greater.", "expectedRevision"));
        }
    }

    private static void ValidateCommandId(CommandId commandId, List<ApplicationProblemDetails> problems)
    {
        if (string.IsNullOrWhiteSpace(commandId.Value))
        {
            problems.Add(Malformed("CommandId is required.", "commandId"));
        }
    }

    private static void ValidateActorContext(ActorContext? actorContext, List<ApplicationProblemDetails> problems)
    {
        if (actorContext is null)
        {
            problems.Add(Malformed("ActorContext is required.", "actorContext"));
            return;
        }

        if (string.IsNullOrWhiteSpace(actorContext.ActorId))
        {
            problems.Add(Malformed("ActorContext.ActorId is required.", "actorContext.actorId"));
        }

        if (string.IsNullOrWhiteSpace(actorContext.SessionId))
        {
            problems.Add(Malformed("ActorContext.SessionId is required.", "actorContext.sessionId"));
        }
    }

    private static void ValidateCommand(ProjectCommand? command, List<ApplicationProblemDetails> problems)
    {
        if (command is null)
        {
            problems.Add(Malformed("Command is required.", "command"));
            return;
        }

        if (string.IsNullOrWhiteSpace(command.CommandType) || !KnownCommandTypes.Contains(command.CommandType))
        {
            problems.Add(Malformed(
                $"Command type '{command.CommandType ?? "<null>"}' is not known.",
                "command.commandType"));
            return;
        }

        switch (command)
        {
            case ImportDocumentCommand c:
                ValidateImportDocument(c, problems);
                break;
            case SetCalibrationCommand c:
                ValidateSetCalibration(c, problems);
                break;
            case UpsertRouteCommand c:
                ValidateUpsertRoute(c, problems);
                break;
            case PlaceComponentCommand c:
                ValidatePlaceComponent(c, problems);
                break;
            case ConnectPortsCommand c:
                ValidateConnectPorts(c, problems);
                break;
            case AcceptProposalCommand c:
                ValidateAcceptProposal(c, problems);
                break;
            case ReviewPropertyCommand c:
                ValidateReviewProperty(c, problems);
                break;
            case SelectAssemblyCommand c:
                ValidateSelectAssembly(c, problems);
                break;
            case ReleaseSnapshotCommand c:
                ValidateReleaseSnapshot(c, problems);
                break;
            default:
                problems.Add(Malformed(
                    $"Command type '{command.CommandType}' is not supported by this validator.",
                    "command.commandType"));
                break;
        }
    }

    private static void ValidateImportDocument(ImportDocumentCommand command, List<ApplicationProblemDetails> problems)
    {
        RequireString(command.UploadHandle, "command.uploadHandle", problems);
    }

    private static void ValidateSetCalibration(SetCalibrationCommand command, List<ApplicationProblemDetails> problems)
    {
        RequireString(command.RegionId, "command.regionId", problems);

        if (command.ControlPoints is null)
        {
            problems.Add(Malformed("command.controlPoints is required.", "command.controlPoints"));
            return;
        }

        for (var i = 0; i < command.ControlPoints.Count; i++)
        {
            var point = command.ControlPoints[i];
            if (point is null)
            {
                problems.Add(Malformed($"command.controlPoints[{i}] is required.", $"command.controlPoints[{i}]"));
                continue;
            }

            RequireFinite(point.SourceX, $"command.controlPoints[{i}].sourceX", problems);
            RequireFinite(point.SourceY, $"command.controlPoints[{i}].sourceY", problems);
            RequireFinite(point.ModelX, $"command.controlPoints[{i}].modelX", problems);
            RequireFinite(point.ModelY, $"command.controlPoints[{i}].modelY", problems);
            if (point.ModelZ.HasValue)
            {
                RequireFinite(point.ModelZ.Value, $"command.controlPoints[{i}].modelZ", problems);
            }
        }
    }

    private static void ValidateUpsertRoute(UpsertRouteCommand command, List<ApplicationProblemDetails> problems)
    {
        RequireOptionalString(command.RouteId, "command.routeId", problems);
        RequireOptionalString(command.StartPortId, "command.startPortId", problems);
        RequireOptionalString(command.EndPortId, "command.endPortId", problems);

        if (command.Vertices is null)
        {
            problems.Add(Malformed("command.vertices is required.", "command.vertices"));
            return;
        }

        for (var i = 0; i < command.Vertices.Count; i++)
        {
            var vertex = command.Vertices[i];
            if (vertex is null)
            {
                problems.Add(Malformed($"command.vertices[{i}] is required.", $"command.vertices[{i}]"));
                continue;
            }

            RequireFinite(vertex.X, $"command.vertices[{i}].x", problems);
            RequireFinite(vertex.Y, $"command.vertices[{i}].y", problems);
            if (vertex.Z.HasValue)
            {
                RequireFinite(vertex.Z.Value, $"command.vertices[{i}].z", problems);
            }
        }
    }

    private static void ValidatePlaceComponent(PlaceComponentCommand command, List<ApplicationProblemDetails> problems)
    {
        RequireOptionalString(command.ComponentId, "command.componentId", problems);
        RequireString(command.CataloguePartId, "command.cataloguePartId", problems);
        RequireString(command.CatalogueReleaseId, "command.catalogueReleaseId", problems);

        if (command.Placement is null)
        {
            problems.Add(Malformed("command.placement is required.", "command.placement"));
        }
        else
        {
            RequireFinite(command.Placement.X, "command.placement.x", problems);
            RequireFinite(command.Placement.Y, "command.placement.y", problems);
            if (command.Placement.Z.HasValue)
            {
                RequireFinite(command.Placement.Z.Value, "command.placement.z", problems);
            }

            if (command.Placement.RotationRadians.HasValue)
            {
                RequireFinite(command.Placement.RotationRadians.Value, "command.placement.rotationRadians", problems);
            }
        }

        if (command.Properties is not null)
        {
            foreach (var kvp in command.Properties)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    problems.Add(Malformed("command.properties keys must not be empty.", "command.properties"));
                }

                if (kvp.Value is null)
                {
                    problems.Add(Malformed($"command.properties['{kvp.Key}'] must not be null.", "command.properties"));
                }
            }
        }
    }

    private static void ValidateConnectPorts(ConnectPortsCommand command, List<ApplicationProblemDetails> problems)
    {
        RequireString(command.PortAId, "command.portAId", problems);
        RequireString(command.PortBId, "command.portBId", problems);
    }

    private static void ValidateAcceptProposal(AcceptProposalCommand command, List<ApplicationProblemDetails> problems)
    {
        RequireString(command.ProposalId, "command.proposalId", problems);
    }

    private static void ValidateReviewProperty(ReviewPropertyCommand command, List<ApplicationProblemDetails> problems)
    {
        RequireString(command.PropertyId, "command.propertyId", problems);
        if (!Enum.IsDefined(command.ReviewState))
        {
            problems.Add(Malformed("command.reviewState is not a defined ReviewState value.", "command.reviewState"));
        }

        RequireOptionalString(command.IssueId, "command.issueId", problems);
    }

    private static void ValidateSelectAssembly(SelectAssemblyCommand command, List<ApplicationProblemDetails> problems)
    {
        RequireString(command.AssemblyContextId, "command.assemblyContextId", problems);
        RequireString(command.SelectedCandidateId, "command.selectedCandidateId", problems);
    }

    private static void ValidateReleaseSnapshot(ReleaseSnapshotCommand command, List<ApplicationProblemDetails> problems)
    {
        RequireString(command.SnapshotId, "command.snapshotId", problems);
        RequireString(command.ReleasePurpose, "command.releasePurpose", problems);

        if (command.AcknowledgedIssueIds is not null)
        {
            for (var i = 0; i < command.AcknowledgedIssueIds.Count; i++)
            {
                RequireString(command.AcknowledgedIssueIds[i], $"command.acknowledgedIssueIds[{i}]", problems);
            }
        }
    }

    private static async Task ValidateWithContextAsync(
        CommandValidationRequest request,
        ICommandValidationContext context,
        List<ApplicationProblemDetails> problems,
        CancellationToken cancellationToken)
    {
        var projectId = request.ProjectId;

        if (!await context.ProjectExistsAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            problems.Add(NotFound("Project was not found in scope.", "projectId"));
            return;
        }

        var currentRevision = await context.GetCurrentRevisionAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (currentRevision.HasValue && currentRevision.Value != request.ExpectedRevision)
        {
            problems.Add(StaleRevision(currentRevision.Value));
            return;
        }

        await ValidateReferencesAsync(request, context, problems, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ValidateReferencesAsync(
        CommandValidationRequest request,
        ICommandValidationContext context,
        List<ApplicationProblemDetails> problems,
        CancellationToken cancellationToken)
    {
        var projectId = request.ProjectId;
        var command = request.Command!;

        switch (command)
        {
            case SetCalibrationCommand c:
                await RequireReferenceExistsAsync(context, projectId, "region", c.RegionId, "command.regionId", problems, cancellationToken).ConfigureAwait(false);
                break;
            case UpsertRouteCommand c:
                await RequireOptionalReferenceExistsAsync(context, projectId, "route", c.RouteId, "command.routeId", problems, cancellationToken).ConfigureAwait(false);
                await RequireOptionalReferenceExistsAsync(context, projectId, "port", c.StartPortId, "command.startPortId", problems, cancellationToken).ConfigureAwait(false);
                await RequireOptionalReferenceExistsAsync(context, projectId, "port", c.EndPortId, "command.endPortId", problems, cancellationToken).ConfigureAwait(false);
                break;
            case PlaceComponentCommand c:
                await RequireOptionalReferenceExistsAsync(context, projectId, "component", c.ComponentId, "command.componentId", problems, cancellationToken).ConfigureAwait(false);
                break;
            case ConnectPortsCommand c:
                await RequireReferenceExistsAsync(context, projectId, "port", c.PortAId, "command.portAId", problems, cancellationToken).ConfigureAwait(false);
                await RequireReferenceExistsAsync(context, projectId, "port", c.PortBId, "command.portBId", problems, cancellationToken).ConfigureAwait(false);
                break;
            case AcceptProposalCommand c:
                await RequireReferenceExistsAsync(context, projectId, "proposal", c.ProposalId, "command.proposalId", problems, cancellationToken).ConfigureAwait(false);
                break;
            case ReviewPropertyCommand c:
                await RequireReferenceExistsAsync(context, projectId, "property", c.PropertyId, "command.propertyId", problems, cancellationToken).ConfigureAwait(false);
                await RequireOptionalReferenceExistsAsync(context, projectId, "issue", c.IssueId, "command.issueId", problems, cancellationToken).ConfigureAwait(false);
                break;
            case SelectAssemblyCommand c:
                await RequireReferenceExistsAsync(context, projectId, "assembly-context", c.AssemblyContextId, "command.assemblyContextId", problems, cancellationToken).ConfigureAwait(false);
                await RequireReferenceExistsAsync(context, projectId, "assembly-candidate", c.SelectedCandidateId, "command.selectedCandidateId", problems, cancellationToken).ConfigureAwait(false);
                break;
            case ReleaseSnapshotCommand c:
                await RequireReferenceExistsAsync(context, projectId, "snapshot", c.SnapshotId, "command.snapshotId", problems, cancellationToken).ConfigureAwait(false);
                if (c.AcknowledgedIssueIds is not null)
                {
                    for (var i = 0; i < c.AcknowledgedIssueIds.Count; i++)
                    {
                        await RequireReferenceExistsAsync(context, projectId, "issue", c.AcknowledgedIssueIds[i], $"command.acknowledgedIssueIds[{i}]", problems, cancellationToken).ConfigureAwait(false);
                    }
                }
                break;
        }
    }

    private static async Task RequireReferenceExistsAsync(
        ICommandValidationContext context,
        ProjectId projectId,
        string referenceKind,
        string referenceId,
        string field,
        List<ApplicationProblemDetails> problems,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            return;
        }

        if (!await context.ReferenceExistsInProjectAsync(projectId, referenceKind, referenceId, cancellationToken).ConfigureAwait(false))
        {
            problems.Add(NotFound(
                $"Referenced {referenceKind} '{referenceId}' was not found in project scope.",
                field));
        }
    }

    private static async Task RequireOptionalReferenceExistsAsync(
        ICommandValidationContext context,
        ProjectId projectId,
        string referenceKind,
        string? referenceId,
        string field,
        List<ApplicationProblemDetails> problems,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            return;
        }

        await RequireReferenceExistsAsync(context, projectId, referenceKind, referenceId, field, problems, cancellationToken).ConfigureAwait(false);
    }

    private static void RequireString(string? value, string field, List<ApplicationProblemDetails> problems)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            problems.Add(Malformed($"{field} is required.", field));
        }
    }

    private static void RequireOptionalString(string? value, string field, List<ApplicationProblemDetails> problems)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            problems.Add(Malformed($"{field} must not be empty when provided.", field));
        }
    }

    private static void RequireFinite(double value, string field, List<ApplicationProblemDetails> problems)
    {
        if (!double.IsFinite(value))
        {
            problems.Add(Malformed($"{field} must be a finite number.", field));
        }
    }

    private static ApplicationProblemDetails Malformed(string detail, string field) =>
        new(
            ApplicationProblemTypes.MalformedPayload,
            "Malformed command payload",
            400,
            detail,
            $"urn:oneclick:validation:{field}",
            ApplicationProblemCodes.MalformedPayload,
            Array.Empty<string>(),
            null);

    private static ApplicationProblemDetails NotFound(string detail, string field, ProjectRevision? currentRevision = null) =>
        new(
            ApplicationProblemTypes.NotFound,
            "Referenced resource not found",
            404,
            detail,
            $"urn:oneclick:validation:{field}",
            ApplicationProblemCodes.NotFound,
            Array.Empty<string>(),
            currentRevision);

    private static ApplicationProblemDetails StaleRevision(ProjectRevision currentRevision) =>
        new(
            ApplicationProblemTypes.StaleRevision,
            "Stale project revision",
            412,
            "Expected revision does not match current project revision.",
            "urn:oneclick:validation:expectedRevision",
            ApplicationProblemCodes.StaleRevision,
            Array.Empty<string>(),
            currentRevision);
}
