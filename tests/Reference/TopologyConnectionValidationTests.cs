using OneClick.Core.Geometry;
using OneClick.Core.Ids;
using OneClick.Core.Topology;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class TopologyConnectionValidationTests
{
    private static readonly NominalSize Dn50 = new("NPS", "2");

    private static Port CreatePort(string id, string interfaceType = "grooved", string? preparation = null, string ownerId = "owner-1") =>
        new(
            new PortId(id),
            ownerId,
            new Point3D(0, 0, 0),
            new Vector3D(1, 0, 0),
            new PortInterfaceSpec(Dn50, interfaceType, preparation));

    [Fact]
    public void ValidateConnection_Resolved_ForCompatibleExplicitPorts()
    {
        var validator = new TopologyConnectionValidator();
        var portA = CreatePort("port-a", "grooved", "roll-groove");
        var portB = CreatePort("port-b", "grooved", "roll-groove");

        var result = validator.ValidateConnection(portA, portB);

        Assert.Equal(TopologyValidationOutcome.Resolved, result.Outcome);
        Assert.Empty(result.Issues);
        Assert.Empty(result.IssueIds);
        Assert.Equal("port-a", result.PortA.Value);
        Assert.Equal("port-b", result.PortB.Value);
    }

    [Fact]
    public void ValidateConnection_CrossingCandidate_IsNotConfirmedConnection()
    {
        var validator = new TopologyConnectionValidator();
        var result = validator.ValidateConnection(
            CreatePort("port-a"),
            CreatePort("port-b"),
            new ConnectionValidationMetadata { IsCrossingCandidate = true });

        Assert.Equal(TopologyValidationOutcome.Infeasible, result.Outcome);
        Assert.Contains(result.Issues, i => i.Code == TopologyIssueCodes.CrossingNotConnection);
        Assert.DoesNotContain(TopologyValidationOutcome.Resolved, new[] { result.Outcome });
    }

    [Fact]
    public void ValidateConnection_ProximityOnlyCandidate_IsNotConfirmedConnection()
    {
        var validator = new TopologyConnectionValidator();
        var result = validator.ValidateConnection(
            CreatePort("port-a"),
            CreatePort("port-b"),
            new ConnectionValidationMetadata { IsProximityOnlyCandidate = true });

        Assert.Equal(TopologyValidationOutcome.Infeasible, result.Outcome);
        Assert.Contains(result.Issues, i => i.Code == TopologyIssueCodes.ProximityNotConnection);
    }

    [Fact]
    public void ValidateConnection_EndpointsNotExplicitlyIdentified_IsRejected()
    {
        var validator = new TopologyConnectionValidator();
        var result = validator.ValidateConnection(
            CreatePort("port-a"),
            CreatePort("port-b"),
            new ConnectionValidationMetadata { EndpointsExplicitlyIdentified = false });

        Assert.Equal(TopologyValidationOutcome.Infeasible, result.Outcome);
        Assert.Contains(result.Issues, i => i.Code == TopologyIssueCodes.EndpointNotExplicitlyIdentified);
    }

    [Fact]
    public void ValidateConnection_MissingPort_IsRejectedWithTypedIssue()
    {
        var validator = new TopologyConnectionValidator();
        var result = validator.ValidateConnection(
            null,
            CreatePort("port-b"),
            new ConnectionValidationMetadata());

        Assert.Equal(TopologyValidationOutcome.Infeasible, result.Outcome);
        var issue = Assert.Single(result.Issues, i => i.Code == TopologyIssueCodes.PortNotIdentified && i.Field == "portA");
        Assert.NotEmpty(issue.Message);
    }

    [Fact]
    public void ValidateConnection_IncompatibleInterface_IsRejected()
    {
        var validator = new TopologyConnectionValidator();
        var result = validator.ValidateConnection(
            CreatePort("port-a", "grooved", "roll-groove"),
            CreatePort("port-b", "threaded", "roll-groove"));

        Assert.Equal(TopologyValidationOutcome.Infeasible, result.Outcome);
        Assert.Contains(result.Issues, i => i.Code == TopologyIssueCodes.IncompatibleInterface);
        Assert.DoesNotContain(result.Issues, i => i.Code == TopologyIssueCodes.IncompatiblePreparation);
    }

    [Fact]
    public void ValidateConnection_IncompatiblePreparation_IsRejected()
    {
        var validator = new TopologyConnectionValidator();
        var result = validator.ValidateConnection(
            CreatePort("port-a", "grooved", "roll-groove"),
            CreatePort("port-b", "grooved", "cut-groove"));

        Assert.Equal(TopologyValidationOutcome.Infeasible, result.Outcome);
        Assert.Contains(result.Issues, i => i.Code == TopologyIssueCodes.IncompatiblePreparation);
    }

    [Fact]
    public void ValidateConnection_CompatibilityHook_CanReportUnsupported()
    {
        var validator = new TopologyConnectionValidator();
        var policy = new StubCompatibilityPolicy(PortInterfaceCompatibilityResult.Incompatible(
            new TopologyIssue(TopologyIssueCodes.CompatibilityUnknown, "Compatibility cannot be determined from available data.", "interfaceSpec")));

        var result = validator.ValidateConnection(
            CreatePort("port-a"),
            CreatePort("port-b"),
            new ConnectionValidationMetadata(),
            policy);

        Assert.Equal(TopologyValidationOutcome.Unsupported, result.Outcome);
        Assert.Contains(result.Issues, i => i.Code == TopologyIssueCodes.CompatibilityUnknown);
    }

    [Fact]
    public void ValidateConnection_CompatibilityHook_CanReportIncompatible()
    {
        var validator = new TopologyConnectionValidator();
        var policy = new StubCompatibilityPolicy(PortInterfaceCompatibilityResult.Incompatible(
            new TopologyIssue(TopologyIssueCodes.IncompatibleInterface, "Catalogue reports incompatible interface.", "interfaceSpec")));

        var result = validator.ValidateConnection(
            CreatePort("port-a"),
            CreatePort("port-b"),
            new ConnectionValidationMetadata(),
            policy);

        Assert.Equal(TopologyValidationOutcome.Infeasible, result.Outcome);
        Assert.Contains(result.Issues, i => i.Code == TopologyIssueCodes.IncompatibleInterface);
    }

    [Fact]
    public void TopologyValidation_StaticFacade_MatchesDocsSignatureShape()
    {
        var result = TopologyValidation.ValidateConnection(
            CreatePort("port-a"),
            CreatePort("port-b"),
            new ConnectionValidationMetadata());

        Assert.Equal(TopologyValidationOutcome.Resolved, result.Outcome);
    }

    [Fact]
    public void CandidateResult_TypedIssues_ExposeDerivedIssueIds()
    {
        var result = new CandidateResult(
            new PortId("port-a"),
            new PortId("port-b"),
            TopologyValidationOutcome.Infeasible,
            new[]
            {
                new TopologyIssue(TopologyIssueCodes.CrossingNotConnection, "Crossing is not a connection."),
            });

        Assert.Single(result.Issues);
        Assert.Equal([TopologyIssueCodes.CrossingNotConnection], result.IssueIds);
    }

    [Fact]
    public void ValidateConnection_DoesNotAutoConnect_WhenMetadataFlagsAreAbsentButPortsCompatible()
    {
        // Deterministic check: without crossing/proximity flags and with explicit compatible ports, result is Resolved.
        // The validator never infers crossing/proximity; it only evaluates explicit inputs.
        var validator = new TopologyConnectionValidator();
        var compatible = validator.ValidateConnection(CreatePort("a"), CreatePort("b"), new ConnectionValidationMetadata());
        var crossing = validator.ValidateConnection(CreatePort("a"), CreatePort("b"), new ConnectionValidationMetadata { IsCrossingCandidate = true });

        Assert.Equal(TopologyValidationOutcome.Resolved, compatible.Outcome);
        Assert.Equal(TopologyValidationOutcome.Infeasible, crossing.Outcome);
    }

    private sealed class StubCompatibilityPolicy(PortInterfaceCompatibilityResult result) : IPortInterfaceCompatibilityPolicy
    {
        public PortInterfaceCompatibilityResult Evaluate(Port portA, Port portB) => result;
    }
}
