using OneClick.Core.Ids;

namespace OneClick.Core.Topology;

/// <summary>
/// Deterministic connection validation service. Enforces explicit topology invariants without
/// geometric crossing detection, proximity search, auto-connection, or catalogue storage.
/// </summary>
public sealed class TopologyConnectionValidator
{
    private readonly IPortInterfaceCompatibilityPolicy _defaultCompatibilityPolicy;

    public TopologyConnectionValidator(IPortInterfaceCompatibilityPolicy? defaultCompatibilityPolicy = null)
    {
        _defaultCompatibilityPolicy = defaultCompatibilityPolicy ?? StrictPortInterfaceCompatibilityPolicy.Instance;
    }

    public CandidateResult ValidateConnection(ConnectionValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValidateConnection(request.PortA, request.PortB, request.Metadata, request.CompatibilityPolicy ?? _defaultCompatibilityPolicy);
    }

    public CandidateResult ValidateConnection(
        Port? portA,
        Port? portB,
        ConnectionValidationMetadata? metadata = null,
        IPortInterfaceCompatibilityPolicy? compatibilityPolicy = null)
    {
        metadata ??= new ConnectionValidationMetadata();
        compatibilityPolicy ??= _defaultCompatibilityPolicy;

        var issues = new List<TopologyIssue>();

        if (portA is null)
        {
            issues.Add(new TopologyIssue(
                TopologyIssueCodes.PortNotIdentified,
                "Port A must be explicitly identified.",
                "portA"));
        }

        if (portB is null)
        {
            issues.Add(new TopologyIssue(
                TopologyIssueCodes.PortNotIdentified,
                "Port B must be explicitly identified.",
                "portB"));
        }

        if (!metadata.EndpointsExplicitlyIdentified)
        {
            issues.Add(new TopologyIssue(
                TopologyIssueCodes.EndpointNotExplicitlyIdentified,
                "Endpoints must be explicitly identified; proximity or inferred candidates are not confirmed connections.",
                "metadata.endpointsExplicitlyIdentified"));
        }

        if (metadata.IsCrossingCandidate)
        {
            issues.Add(new TopologyIssue(
                TopologyIssueCodes.CrossingNotConnection,
                "A plan crossing is not a connection. Explicit endpoint identification and validation are required.",
                "metadata.isCrossingCandidate"));
        }

        if (metadata.IsProximityOnlyCandidate)
        {
            issues.Add(new TopologyIssue(
                TopologyIssueCodes.ProximityNotConnection,
                "A nearby endpoint is only a candidate and not a confirmed connection.",
                "metadata.isProximityOnlyCandidate"));
        }

        if (portA is not null && portB is not null && string.Equals(portA.Id.Value, portB.Id.Value, StringComparison.Ordinal))
        {
            issues.Add(new TopologyIssue(
                TopologyIssueCodes.SamePort,
                "A connection candidate requires two distinct ports.",
                "portB"));
        }

        if (portA is not null && portB is not null)
        {
            var compatibility = compatibilityPolicy.Evaluate(portA, portB);
            if (!compatibility.IsCompatible)
            {
                foreach (var issue in compatibility.Issues)
                    issues.Add(issue);

                if (compatibility.Issues.Count == 0)
                {
                    issues.Add(new TopologyIssue(
                        TopologyIssueCodes.IncompatibleInterface,
                        "Port interfaces/preparations are incompatible for a confirmed connection.",
                        "interfaceSpec"));
                }
            }
        }

        if (issues.Count > 0)
        {
            var outcome = MapOutcome(issues, metadata);
            var portAId = portA?.Id ?? new PortId("unidentified-port-a");
            var portBId = portB?.Id ?? new PortId("unidentified-port-b");
            if (string.Equals(portAId.Value, portBId.Value, StringComparison.Ordinal))
                portBId = new PortId("unidentified-port-b");

            return new CandidateResult(portAId, portBId, outcome, issues);
        }

        return new CandidateResult(portA!.Id, portB!.Id, TopologyValidationOutcome.Resolved, Array.Empty<TopologyIssue>());
    }

    private static TopologyValidationOutcome MapOutcome(IReadOnlyList<TopologyIssue> issues, ConnectionValidationMetadata metadata)
    {
        if (metadata.IsCrossingCandidate || metadata.IsProximityOnlyCandidate || !metadata.EndpointsExplicitlyIdentified)
            return TopologyValidationOutcome.Infeasible;

        var hasUnknownCompatibility = false;
        var hasIncompatibility = false;
        var hasMissingIdentity = false;

        foreach (var issue in issues)
        {
            if (issue.Code == TopologyIssueCodes.CompatibilityUnknown)
                hasUnknownCompatibility = true;
            else if (issue.Code == TopologyIssueCodes.IncompatibleInterface || issue.Code == TopologyIssueCodes.IncompatiblePreparation)
                hasIncompatibility = true;
            else if (issue.Code == TopologyIssueCodes.PortNotIdentified ||
                     issue.Code == TopologyIssueCodes.EndpointNotExplicitlyIdentified ||
                     issue.Code == TopologyIssueCodes.SamePort)
                hasMissingIdentity = true;
        }

        if (hasMissingIdentity)
            return TopologyValidationOutcome.Infeasible;

        if (hasUnknownCompatibility && !hasIncompatibility)
            return TopologyValidationOutcome.Unsupported;

        if (hasIncompatibility)
            return TopologyValidationOutcome.Infeasible;

        return TopologyValidationOutcome.Infeasible;
    }
}
