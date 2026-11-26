using OneClick.Core.Geometry;
using OneClick.Core.Ids;
using OneClick.Core.Topology;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class TopologyFoundationTests
{
    private static readonly NominalSize Dn50 = new("NPS", "2");

    private static PortInterfaceSpec CreateInterfaceSpec() => new(Dn50, "grooved");

    private static Port CreatePort(string id, string ownerId, Point3D? position = null, Vector3D? normal = null) =>
        new(
            new PortId(id),
            ownerId,
            position ?? new Point3D(0, 0, 0),
            normal ?? new Vector3D(1, 0, 0),
            CreateInterfaceSpec());

    [Fact]
    public void Port_StoresRequiredModelContract()
    {
        var port = new Port(
            new PortId("port-1"),
            "component-1",
            new Point3D(1, 2, 3),
            new Vector3D(0, 0, 1),
            new PortInterfaceSpec(new NominalSize("DN", "50"), "flanged", "beveled"),
            clockingRadians: Math.PI / 2d);

        Assert.Equal("port-1", port.Id.Value);
        Assert.Equal("component-1", port.OwnerId);
        Assert.Equal(new Point3D(1, 2, 3), port.Position);
        Assert.Equal(new Vector3D(0, 0, 1), port.OutwardNormal);
        Assert.Equal("DN:50", port.InterfaceSpec.NominalSize.ToString());
        Assert.Equal("flanged", port.InterfaceSpec.InterfaceType);
        Assert.Equal("beveled", port.InterfaceSpec.Preparation);
        Assert.Equal(Math.PI / 2d, port.ClockingRadians);
    }

    [Fact]
    public void Port_AllowsMissingOptionalClocking()
    {
        var port = CreatePort("port-1", "owner-1");

        Assert.Null(port.ClockingRadians);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Port_RequiresOwnerId(string? ownerId)
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            new Port(new PortId("port-1"), ownerId!, new Point3D(0, 0, 0), new Vector3D(1, 0, 0), CreateInterfaceSpec()));
        Assert.Equal("ownerId", ex.ParamName);
    }

    [Fact]
    public void Port_RequiresValidId()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            new Port(default, "owner-1", new Point3D(0, 0, 0), new Vector3D(1, 0, 0), CreateInterfaceSpec()));
        Assert.Equal("id", ex.ParamName);
    }

    [Fact]
    public void Port_RequiresInterfaceSpec()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Port(new PortId("port-1"), "owner-1", new Point3D(0, 0, 0), new Vector3D(1, 0, 0), null!));
    }

    [Theory]
    [InlineData(0d, 0d, 0d)]
    [InlineData(1d, 1d, 0d)]
    [InlineData(2d, 0d, 0d)]
    [InlineData(0.5d, 0d, 0d)]
    public void Port_RequiresUnitOutwardNormal(double x, double y, double z)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Port(new PortId("port-1"), "owner-1", new Point3D(0, 0, 0), new Vector3D(x, y, z), CreateInterfaceSpec()));

        Assert.Equal("outwardNormal", ex.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Port_RejectsNonFiniteClocking(double clocking)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Port(new PortId("port-1"), "owner-1", new Point3D(0, 0, 0), new Vector3D(1, 0, 0), CreateInterfaceSpec(), clocking));

        Assert.Equal("clockingRadians", ex.ParamName);
    }

    [Fact]
    public void PortInterfaceSpec_RequiresNominalSizeAndInterfaceType()
    {
        Assert.Throws<ArgumentNullException>(() => new PortInterfaceSpec(null!, "grooved"));
        var ex = Assert.ThrowsAny<ArgumentException>(() => new PortInterfaceSpec(Dn50, " "));
        Assert.Equal("interfaceType", ex.ParamName);
    }

    [Fact]
    public void PortInterfaceSpec_RejectsBlankPreparationWhenProvided()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new PortInterfaceSpec(Dn50, "grooved", " "));
        Assert.Equal("preparation", ex.ParamName);
    }

    [Fact]
    public void Joint_StoresConfirmedPortsAndOwnership()
    {
        var joint = new Joint(
            new JointId("joint-1"),
            [new PortId("port-a"), new PortId("port-b")],
            "adapter-grooved-1",
            new AssemblyId("assembly-1"));

        Assert.Equal("joint-1", joint.Id.Value);
        Assert.Equal(["port-a", "port-b"], joint.ConfirmedPortIds.Select(p => p.Value));
        Assert.Equal("adapter-grooved-1", joint.SelectedJoiningAdapterId);
        Assert.Equal("assembly-1", joint.AssemblyOwnershipId.Value);
    }

    [Fact]
    public void Joint_RequiresAtLeastTwoConfirmedPorts()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Joint(new JointId("joint-1"), [new PortId("port-a")], "adapter", new AssemblyId("assembly-1")));

        Assert.Equal("portIds", ex.ParamName);
    }

    [Fact]
    public void Joint_RejectsDuplicatedConfirmedPorts()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Joint(
                new JointId("joint-1"),
                [new PortId("port-a"), new PortId("port-a")],
                "adapter",
                new AssemblyId("assembly-1")));

        Assert.Equal("portIds", ex.ParamName);
    }

    [Fact]
    public void Joint_RequiresJoiningAdapterAndAssemblyOwnership()
    {
        var ports = new[] { new PortId("port-a"), new PortId("port-b") };

        var adapterEx = Assert.ThrowsAny<ArgumentException>(() =>
            new Joint(new JointId("joint-1"), ports, "", new AssemblyId("assembly-1")));
        Assert.Equal("selectedJoiningAdapterId", adapterEx.ParamName);

        var ownerEx = Assert.ThrowsAny<ArgumentException>(() =>
            new Joint(new JointId("joint-1"), ports, "adapter", default));
        Assert.Equal("assemblyOwnershipId", ownerEx.ParamName);
    }

    [Fact]
    public void Joint_ConfirmedPortsAreCopied()
    {
        var ports = new List<PortId> { new("port-a"), new("port-b") };
        var joint = new Joint(new JointId("joint-1"), ports, "adapter", new AssemblyId("assembly-1"));

        ports[0] = new PortId("mutated");

        Assert.Equal("port-a", joint.ConfirmedPortIds[0].Value);
    }

    [Fact]
    public void ConnectionCandidate_DefaultsToCandidateLifecycle()
    {
        var candidate = new ConnectionCandidate(
            new ConnectionCandidateId("cand-1"),
            new PortId("port-a"),
            new PortId("port-b"));

        Assert.Equal(ConnectionCandidateStatus.Candidate, candidate.Status);
        Assert.Null(candidate.ValidationOutcome);
        Assert.Null(candidate.Reason);
    }

    [Fact]
    public void ConnectionCandidate_IsExplicitlyConfirmedOrRejected()
    {
        var confirmed = new ConnectionCandidate(
            new ConnectionCandidateId("cand-1"),
            new PortId("port-a"),
            new PortId("port-b"),
            ConnectionCandidateStatus.Confirmed,
            TopologyValidationOutcome.Resolved,
            "Validated by explicit review");

        var rejected = new ConnectionCandidate(
            new ConnectionCandidateId("cand-2"),
            new PortId("port-a"),
            new PortId("port-c"),
            ConnectionCandidateStatus.Rejected,
            TopologyValidationOutcome.Infeasible,
            "Crossing only");

        Assert.Equal(ConnectionCandidateStatus.Confirmed, confirmed.Status);
        Assert.Equal(TopologyValidationOutcome.Resolved, confirmed.ValidationOutcome);
        Assert.Equal(ConnectionCandidateStatus.Rejected, rejected.Status);
        Assert.Equal(TopologyValidationOutcome.Infeasible, rejected.ValidationOutcome);
    }

    [Fact]
    public void ConnectionCandidate_RejectsSamePort()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new ConnectionCandidate(new ConnectionCandidateId("cand-1"), new PortId("port-a"), new PortId("port-a")));

        Assert.Equal("portB", ex.ParamName);
    }

    [Fact]
    public void ConnectionCandidate_RejectsUndefinedStatusOrOutcome()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ConnectionCandidate(
                new ConnectionCandidateId("cand-1"),
                new PortId("port-a"),
                new PortId("port-b"),
                (ConnectionCandidateStatus)999));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ConnectionCandidate(
                new ConnectionCandidateId("cand-1"),
                new PortId("port-a"),
                new PortId("port-b"),
                ConnectionCandidateStatus.Candidate,
                (TopologyValidationOutcome)999));
    }

    [Fact]
    public void CandidateResult_ModelsExplicitTopologyOutcomes()
    {
        var result = new CandidateResult(
            new PortId("port-a"),
            new PortId("port-b"),
            TopologyValidationOutcome.Alternatives,
            ["issue-1", "issue-2"]);

        Assert.Equal(TopologyValidationOutcome.Alternatives, result.Outcome);
        Assert.Equal(["issue-1", "issue-2"], result.IssueIds);
    }

    [Fact]
    public void CandidateResult_DefaultsIssueIdsToEmptyAndCopiesInput()
    {
        var issues = new List<string> { "issue-1" };
        var result = new CandidateResult(new PortId("port-a"), new PortId("port-b"), TopologyValidationOutcome.Incomplete, issues);
        issues[0] = "mutated";

        Assert.Equal(["issue-1"], result.IssueIds);

        var noIssues = new CandidateResult(new PortId("port-a"), new PortId("port-b"), TopologyValidationOutcome.Unsupported);
        Assert.Empty(noIssues.IssueIds);
    }

    [Fact]
    public void CandidateResult_RejectsSamePortOrUndefinedOutcome()
    {
        Assert.Throws<ArgumentException>(() =>
            new CandidateResult(new PortId("port-a"), new PortId("port-a"), TopologyValidationOutcome.Resolved));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CandidateResult(new PortId("port-a"), new PortId("port-b"), (TopologyValidationOutcome)999));
    }

    [Fact]
    public void TopologyValidationOutcomes_AreExplicit()
    {
        var expected = new[]
        {
            TopologyValidationOutcome.Resolved,
            TopologyValidationOutcome.Alternatives,
            TopologyValidationOutcome.Infeasible,
            TopologyValidationOutcome.Unsupported,
            TopologyValidationOutcome.Incomplete,
        };

        Assert.Equal(expected, Enum.GetValues<TopologyValidationOutcome>().OrderBy(x => (int)x));
    }

    [Fact]
    public void ConnectionCandidateStatuses_AreExplicit()
    {
        var expected = new[]
        {
            ConnectionCandidateStatus.Candidate,
            ConnectionCandidateStatus.Confirmed,
            ConnectionCandidateStatus.Rejected,
        };

        Assert.Equal(expected, Enum.GetValues<ConnectionCandidateStatus>().OrderBy(x => (int)x));
    }

    [Fact]
    public void NearbyEndpoint_IsModeledAsCandidate_NotConfirmedJoint()
    {
        var candidate = new ConnectionCandidate(
            new ConnectionCandidateId("cand-nearby"),
            new PortId("port-a"),
            new PortId("port-b"));

        Assert.Equal(ConnectionCandidateStatus.Candidate, candidate.Status);
        Assert.NotEqual(ConnectionCandidateStatus.Confirmed, candidate.Status);

        // No joint is created by candidate construction; confirmed joints require explicit ports and ownership.
        var joint = new Joint(new JointId("joint-1"), [candidate.PortA, candidate.PortB], "adapter", new AssemblyId("assembly-1"));
        Assert.Equal(candidate.PortA, joint.ConfirmedPortIds[0]);
    }

    [Fact]
    public void Crossing_IsNotAConnection_CandidateCanBeRejectedWithoutJoint()
    {
        var crossingCandidate = new ConnectionCandidate(
            new ConnectionCandidateId("cand-crossing"),
            new PortId("port-a"),
            new PortId("port-b"),
            ConnectionCandidateStatus.Rejected,
            TopologyValidationOutcome.Infeasible,
            "Plan crossing without confirmed connection evidence");

        Assert.Equal(ConnectionCandidateStatus.Rejected, crossingCandidate.Status);
        Assert.Equal(TopologyValidationOutcome.Infeasible, crossingCandidate.ValidationOutcome);
        Assert.Contains("crossing", crossingCandidate.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
