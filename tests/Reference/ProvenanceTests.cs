using OneClick.Core.Provenance;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

using ProvenanceQuantityContribution = OneClick.Core.Provenance.QuantityContribution;

namespace OneClick.Tests.Reference;

public sealed class ProvenanceTests
{
    [Fact]
    public void Ledger_MatchesDomainModel()
    {
        var expected = new[]
        {
            Ledger.Route,
            Ledger.Fabrication,
            Ledger.Hydraulic,
            Ledger.Procurement,
        };

        Assert.Equal(expected, Enum.GetValues<Ledger>().OrderBy(x => (int)x));
    }

    [Fact]
    public void QuantityContributionId_ValidatesAndFormats()
    {
        var id = new QuantityContributionId("qc-1");

        Assert.Equal("qc-1", id.Value);
        Assert.Equal("qc-1", id.ToString());
        Assert.ThrowsAny<ArgumentException>(() => new QuantityContributionId(""));
        Assert.ThrowsAny<ArgumentException>(() => new QuantityContributionId(" "));
    }

    [Fact]
    public void QuantityContributionId_NewCreatesStableFormattedValue()
    {
        var a = QuantityContributionId.New();
        var b = QuantityContributionId.New();

        Assert.NotEqual(a, b);
        Assert.True(Guid.TryParseExact(a.Value, "D", out _));
    }

    [Fact]
    public void InputReference_CapturesVersionedInputRef()
    {
        var input = new InputReference("document-revision", "rev-1", "sha256:abc");

        Assert.Equal("document-revision", input.Kind);
        Assert.Equal("rev-1", input.Id);
        Assert.Equal("sha256:abc", input.Version);
    }

    [Theory]
    [InlineData("", "id", "v", "kind")]
    [InlineData("kind", "", "v", "id")]
    [InlineData("kind", "id", "", "version")]
    [InlineData(" ", "id", "v", "kind")]
    public void InputReference_RequiresFields(string kind, string id, string version, string expectedParam)
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() => new InputReference(kind, id, version));
        Assert.Equal(expectedParam, ex.ParamName);
    }

    [Fact]
    public void InputFingerprint_IsValidatedValueObject()
    {
        var fingerprint = new InputFingerprint("fp-123");

        Assert.Equal("fp-123", fingerprint.Value);
        Assert.Equal("fp-123", fingerprint.ToString());
        Assert.ThrowsAny<ArgumentException>(() => new InputFingerprint(""));
    }

    [Fact]
    public void ResultProvenance_CarriesInputVersionsMethodVersionAndContributions()
    {
        var inputs = new List<InputReference> { new("project", "p1", "rev-7") };
        var sources = new List<string> { "obs-1" };
        var contributions = new List<QuantityContributionId> { new("qc-1") };

        var provenance = new ResultProvenance("Q01.1.0", inputs, sources, contributions);

        Assert.Equal("Q01.1.0", provenance.MethodVersion);
        Assert.Equal(inputs, provenance.Inputs);
        Assert.Equal(sources, provenance.SourceRefs);
        Assert.Equal(contributions, provenance.ContributionIds);

        inputs.Add(new InputReference("catalogue", "c1", "v2"));
        sources[0] = "mutated";
        contributions.Add(new QuantityContributionId("qc-2"));

        Assert.Single(provenance.Inputs);
        Assert.Equal(["obs-1"], provenance.SourceRefs);
        Assert.Single(provenance.ContributionIds);
    }

    [Fact]
    public void ResultProvenance_RequiresMethodVersionAndValidCollections()
    {
        var inputs = new[] { new InputReference("project", "p1", "1") };
        var sources = new[] { "obs-1" };
        var contributions = new[] { new QuantityContributionId("qc-1") };

        Assert.ThrowsAny<ArgumentException>(() => new ResultProvenance("", inputs, sources, contributions));
        Assert.Throws<ArgumentNullException>(() => new ResultProvenance("m1", null!, sources, contributions));
        Assert.Throws<ArgumentNullException>(() => new ResultProvenance("m1", inputs, null!, contributions));
        Assert.Throws<ArgumentNullException>(() => new ResultProvenance("m1", inputs, sources, null!));
        Assert.Throws<ArgumentException>(() => new ResultProvenance("m1", new InputReference[] { null! }, sources, contributions));
        Assert.ThrowsAny<ArgumentException>(() => new ResultProvenance("m1", inputs, [" "], contributions));
        Assert.Throws<ArgumentException>(() => new ResultProvenance("m1", inputs, sources, [default]));
    }

    [Fact]
    public void DerivedResult_ExposesContractWithProvenance()
    {
        var provenance = CreateProvenance();
        IDerivedResult result = new DerivedResult<KnownValue>(
            new KnownValue(1.5, SiUnit.Meter, ["obs-1"], "Q01.1.0"),
            provenance);

        Assert.Same(provenance, result.Provenance);
        Assert.Equal("Q01.1.0", result.Provenance.MethodVersion);
        Assert.NotEmpty(result.Provenance.Inputs);
        Assert.NotEmpty(result.Provenance.ContributionIds);
    }

    [Fact]
    public void DerivedResult_RejectsNullValueOrProvenance()
    {
        var provenance = CreateProvenance();

        Assert.Throws<ArgumentNullException>(() => new DerivedResult<KnownValue>(null!, provenance));
        Assert.Throws<ArgumentNullException>(() => new DerivedResult<KnownValue>(
            new KnownValue(1, SiUnit.Meter, ["obs"], "m1"),
            null!));
    }

    [Fact]
    public void QuantityContribution_CarriesDomainAlignedIdentityAndProvenance()
    {
        var value = new KnownValue(12.5, SiUnit.Meter, ["obs-route-1"], "Q01.1.0");
        var contribution = new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-1"),
            "route-1",
            Ledger.Route,
            "main/steel",
            value,
            ["obs-route-1"],
            "Q01.1.0");

        Assert.Equal("qc-1", contribution.Id.Value);
        Assert.Equal("route-1", contribution.OwnerId);
        Assert.Equal(Ledger.Route, contribution.Ledger);
        Assert.Equal("main/steel", contribution.GroupKey);
        Assert.Same(value, contribution.QualifiedValue);
        Assert.Equal(["obs-route-1"], contribution.SourceRefs);
        Assert.Equal("Q01.1.0", contribution.MethodVersion);
    }

    [Fact]
    public void QuantityContribution_RejectsInvalidIdentityOrValue()
    {
        var value = new KnownValue(1, SiUnit.Meter, ["obs"], "m1");

        Assert.ThrowsAny<ArgumentException>(() => new ProvenanceQuantityContribution(
            default, "owner", Ledger.Route, "group", value, ["obs"], "m1"));
        Assert.ThrowsAny<ArgumentException>(() => new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-1"), "", Ledger.Route, "group", value, ["obs"], "m1"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-1"), "owner", (Ledger)999, "group", value, ["obs"], "m1"));
        Assert.ThrowsAny<ArgumentException>(() => new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-1"), "owner", Ledger.Route, " ", value, ["obs"], "m1"));
        Assert.Throws<ArgumentNullException>(() => new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-1"), "owner", Ledger.Route, "group", null!, ["obs"], "m1"));
        Assert.ThrowsAny<ArgumentException>(() => new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-1"), "owner", Ledger.Route, "group", value, ["obs"], ""));
    }

    [Fact]
    public void QuantityContribution_DefensivelyCopiesSourceRefs()
    {
        var refs = new List<string> { "obs-1" };
        var contribution = new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-1"),
            "owner",
            Ledger.Fabrication,
            "group",
            new KnownValue(1, SiUnit.Meter, ["obs-1"], "m1"),
            refs,
            "m1");

        refs[0] = "mutated";

        Assert.Equal(["obs-1"], contribution.SourceRefs);
        Assert.ThrowsAny<ArgumentException>(() => new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-2"),
            "owner",
            Ledger.Fabrication,
            "group",
            new KnownValue(1, SiUnit.Meter, ["obs-1"], "m1"),
            [" "],
            "m1"));
    }

    [Fact]
    public void QuantityContribution_PreservesUnknownQualifiedValue()
    {
        var unknown = new UnknownValue(SiUnit.Meter, ["obs-1"], "m1", "missing elevation", ["issue-1"]);
        var contribution = new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-u"),
            "owner",
            Ledger.Hydraulic,
            "group",
            unknown,
            ["obs-1"],
            "m1");

        var qualified = Assert.IsType<UnknownValue>(contribution.QualifiedValue);
        Assert.Null(qualified.Value);
        Assert.Equal("missing elevation", qualified.Reason);
    }

    private static ResultProvenance CreateProvenance() =>
        new(
            "Q01.1.0",
            [new InputReference("project", "p1", "rev-1")],
            ["obs-1"],
            [new QuantityContributionId("qc-1")]);
}
