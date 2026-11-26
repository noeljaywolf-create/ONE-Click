using OneClick.Core.Provenance;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

using FormulaQuantityContribution = OneClick.Core.Formulas.QuantityContribution;
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

    [Fact]
    public void QuantityContribution_FromAggregationInput_MapsCanonicalFields()
    {
        var aggregation = new FormulaQuantityContribution("route-a", "route", "main/steel", 12.5d, SiUnit.Meter);

        var contribution = ProvenanceQuantityContribution.FromAggregationInput(
            aggregation,
            new QuantityContributionId("qc-agg-1"),
            Ledger.Route,
            ["obs-route-1"],
            "Q01.1.0");

        Assert.Equal("qc-agg-1", contribution.Id.Value);
        Assert.Equal("route-a", contribution.OwnerId);
        Assert.Equal(Ledger.Route, contribution.Ledger);
        Assert.Equal("main/steel", contribution.GroupKey);
        var qualified = Assert.IsType<KnownValue>(contribution.QualifiedValue);
        Assert.Equal(12.5d, qualified.Value);
        Assert.Equal(SiUnit.Meter, qualified.Unit);
        Assert.Equal(["obs-route-1"], qualified.SourceRefs);
        Assert.Equal("Q01.1.0", qualified.MethodVersion);
        Assert.Equal(["obs-route-1"], contribution.SourceRefs);
        Assert.Equal("Q01.1.0", contribution.MethodVersion);
    }

    [Fact]
    public void FormulaQuantityContribution_ToProvenance_PreservesUnknownAndProvisionalSemanticsByExplicitValue()
    {
        // Aggregation inputs are finite known quantities; canonical mapping produces a known qualified value.
        // Unknown/provisional semantics remain represented by TaggedValue on the canonical contribution.
        var aggregation = new FormulaQuantityContribution("part-1", "procurement", "hanger", 0.4d, SiUnit.Meter, includedByOwnerId: "asm-1");
        var canonicalUnknown = new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-u"),
            aggregation.OwnerId,
            Ledger.Procurement,
            aggregation.SpecGroup,
            new UnknownValue(SiUnit.Meter, ["obs-1"], "Q01.1.0", "missing measurement", ["issue-1"]),
            ["obs-1"],
            "Q01.1.0");

        var mapped = aggregation.ToProvenance(
            new QuantityContributionId("qc-k"),
            Ledger.Procurement,
            ["obs-1"],
            "Q01.1.0");

        var known = Assert.IsType<KnownValue>(mapped.QualifiedValue);
        Assert.Equal(0.4d, known.Value);
        var unknown = Assert.IsType<UnknownValue>(canonicalUnknown.QualifiedValue);
        Assert.Null(unknown.Value);
        Assert.Equal("missing measurement", unknown.Reason);
        Assert.Contains("issue-1", unknown.IssueRefs);
    }

    [Fact]
    public void QuantityContribution_Bridge_RejectsInvalidCanonicalContext()
    {
        var aggregation = new FormulaQuantityContribution("owner", "route", "group", 1d, SiUnit.Meter);

        Assert.ThrowsAny<ArgumentException>(() => aggregation.ToProvenance(default, Ledger.Route, ["obs"], "Q01.1.0"));
        Assert.Throws<ArgumentOutOfRangeException>(() => aggregation.ToProvenance(new QuantityContributionId("qc-1"), (Ledger)999, ["obs"], "Q01.1.0"));
        Assert.Throws<ArgumentNullException>(() => aggregation.ToProvenance(new QuantityContributionId("qc-1"), Ledger.Route, null!, "Q01.1.0"));
        Assert.ThrowsAny<ArgumentException>(() => aggregation.ToProvenance(new QuantityContributionId("qc-1"), Ledger.Route, ["obs"], ""));
        Assert.Throws<ArgumentNullException>(() => ProvenanceQuantityContribution.FromAggregationInput(null!, new QuantityContributionId("qc-1"), Ledger.Route, ["obs"], "Q01.1.0"));
    }

    [Fact]
    public void CalculationResult_CarriesValueProvenanceIssuesWarningsAndContributions()
    {
        var provenance = CreateProvenance();
        var contribution = new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-1"),
            "owner",
            Ledger.Route,
            "group",
            new KnownValue(1d, SiUnit.Meter, ["obs-1"], "Q01.1.0"),
            ["obs-1"],
            "Q01.1.0");

        var result = new CalculationResult<KnownValue>(
            new KnownValue(1d, SiUnit.Meter, ["obs-1"], "Q01.1.0"),
            provenance,
            warnings: ["warning:1"],
            issues: ["issue:1"],
            contributions: [contribution]);

        Assert.IsAssignableFrom<IDerivedResult>(result);
        Assert.Same(provenance, result.Provenance);
        Assert.Equal(1d, result.Value.Value);
        Assert.Equal(["warning:1"], result.Warnings);
        Assert.Equal(["issue:1"], result.Issues);
        Assert.Single(result.Contributions);
        Assert.Equal("qc-1", result.Contributions[0].Id.Value);
    }

    [Fact]
    public void CalculationResult_DefaultsOptionalCollections_AndDefensivelyCopies()
    {
        var warnings = new List<string> { "w1" };
        var issues = new List<string> { "i1" };
        var contributions = new List<ProvenanceQuantityContribution>
        {
            new(
                new QuantityContributionId("qc-1"),
                "owner",
                Ledger.Route,
                "group",
                new KnownValue(1d, SiUnit.Meter, ["obs-1"], "Q01.1.0"),
                ["obs-1"],
                "Q01.1.0"),
        };

        var result = new CalculationResult<KnownValue>(
            new KnownValue(1d, SiUnit.Meter, ["obs-1"], "Q01.1.0"),
            CreateProvenance(),
            warnings,
            issues,
            contributions);

        warnings.Add("w2");
        issues[0] = "mutated";
        contributions.Clear();

        Assert.Equal(["w1"], result.Warnings);
        Assert.Equal(["i1"], result.Issues);
        Assert.Single(result.Contributions);

        var minimal = new CalculationResult<KnownValue>(
            new KnownValue(2d, SiUnit.Meter, ["obs-1"], "Q01.1.0"),
            CreateProvenance());
        Assert.Empty(minimal.Warnings);
        Assert.Empty(minimal.Issues);
        Assert.Empty(minimal.Contributions);
    }

    [Fact]
    public void CalculationResult_RejectsInvalidInput()
    {
        var provenance = CreateProvenance();
        var value = new KnownValue(1d, SiUnit.Meter, ["obs-1"], "Q01.1.0");

        Assert.Throws<ArgumentNullException>(() => new CalculationResult<KnownValue>(null!, provenance));
        Assert.Throws<ArgumentNullException>(() => new CalculationResult<KnownValue>(value, null!));
        Assert.ThrowsAny<ArgumentException>(() => new CalculationResult<KnownValue>(value, provenance, warnings: [" "]));
        Assert.ThrowsAny<ArgumentException>(() => new CalculationResult<KnownValue>(value, provenance, issues: [""]));
        Assert.Throws<ArgumentException>(() => new CalculationResult<KnownValue>(value, provenance, contributions: [null!]));
    }

    [Fact]
    public void CalculationResult_InteropsWithDerivedResult()
    {
        var derived = new DerivedResult<KnownValue>(
            new KnownValue(3d, SiUnit.Meter, ["obs-1"], "Q01.1.0"),
            CreateProvenance());

        var envelope = CalculationResult<KnownValue>.FromDerivedResult(derived, warnings: ["w"]);
        Assert.Same(derived.Value, envelope.Value);
        Assert.Same(derived.Provenance, envelope.Provenance);
        Assert.Equal(["w"], envelope.Warnings);

        var roundTrip = envelope.ToDerivedResult();
        Assert.IsType<DerivedResult<KnownValue>>(roundTrip);
        Assert.Same(envelope.Value, roundTrip.Value);
        Assert.Same(envelope.Provenance, roundTrip.Provenance);

        Assert.Throws<ArgumentNullException>(() => CalculationResult<KnownValue>.FromDerivedResult(null!));
    }

    [Fact]
    public void CalculationResult_PreservesUnknownQualifiedContribution()
    {
        var unknownContribution = new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-u"),
            "owner",
            Ledger.Hydraulic,
            "group",
            new UnknownValue(SiUnit.Meter, ["obs-1"], "Q01.1.0", "missing", ["issue-1"]),
            ["obs-1"],
            "Q01.1.0");

        var result = new CalculationResult<TaggedValue>(
            new UnknownValue(SiUnit.Meter, ["obs-1"], "Q01.1.0", "missing", ["issue-1"]),
            CreateProvenance(),
            issues: ["issue-1"],
            contributions: [unknownContribution]);

        var value = Assert.IsType<UnknownValue>(result.Value);
        Assert.Null(value.Value);
        var contributionValue = Assert.IsType<UnknownValue>(result.Contributions[0].QualifiedValue);
        Assert.Null(contributionValue.Value);
        Assert.Equal("missing", contributionValue.Reason);
    }

    [Fact]
    public void QuantityContributionAdapter_ToCanonical_MapsAggregationContribution()
    {
        var aggregation = new FormulaQuantityContribution("route-a", "route", "main/steel", 12.5d, SiUnit.Meter, includedByOwnerId: "asm-1");

        var canonical = QuantityContributionAdapter.ToCanonical(
            aggregation,
            new QuantityContributionId("qc-1"),
            Ledger.Route,
            ["obs-route-1"],
            "Q01.1.0");

        Assert.Equal("qc-1", canonical.Id.Value);
        Assert.Equal("route-a", canonical.OwnerId);
        Assert.Equal(Ledger.Route, canonical.Ledger);
        Assert.Equal("main/steel", canonical.GroupKey);
        var qualified = Assert.IsType<KnownValue>(canonical.QualifiedValue);
        Assert.Equal(12.5d, qualified.Value);
        Assert.Equal(SiUnit.Meter, qualified.Unit);
        Assert.Equal(["obs-route-1"], canonical.SourceRefs);
        Assert.Equal("Q01.1.0", canonical.MethodVersion);
    }

    [Fact]
    public void QuantityContributionAdapter_ToCanonical_BatchMapsInOrder()
    {
        var inputs = new[]
        {
            new FormulaQuantityContribution("a", "route", "g", 1d, SiUnit.Meter),
            new FormulaQuantityContribution("b", "route", "g", 2d, SiUnit.Meter),
        };

        var mapped = QuantityContributionAdapter.ToCanonical(
            inputs,
            Ledger.Route,
            ["obs"],
            "Q01.1.0",
            c => new QuantityContributionId($"qc-{c.OwnerId}"));

        Assert.Equal(2, mapped.Count);
        Assert.Equal("qc-a", mapped[0].Id.Value);
        Assert.Equal("qc-b", mapped[1].Id.Value);
        Assert.Equal(1d, Assert.IsType<KnownValue>(mapped[0].QualifiedValue).Value);
        Assert.Equal(2d, Assert.IsType<KnownValue>(mapped[1].QualifiedValue).Value);
    }

    [Fact]
    public void QuantityContributionAdapter_ParseLedger_IsExplicit()
    {
        Assert.True(QuantityContributionAdapter.TryParseLedger("route", out var route));
        Assert.Equal(Ledger.Route, route);
        Assert.True(QuantityContributionAdapter.TryParseLedger(" Fabrication ", out var fabrication));
        Assert.Equal(Ledger.Fabrication, fabrication);
        Assert.False(QuantityContributionAdapter.TryParseLedger("synthetic-cut", out _));
        Assert.False(QuantityContributionAdapter.TryParseLedger("", out _));

        Assert.Equal(Ledger.Procurement, QuantityContributionAdapter.ParseLedger("procurement"));
        var ex = Assert.Throws<ArgumentException>(() => QuantityContributionAdapter.ParseLedger("synthetic-cut"));
        Assert.Contains("synthetic-cut", ex.Message);
    }

    [Fact]
    public void FormulaProvenance_WrapCalculation_CarriesDiagnosticsAndContributions()
    {
        var contribution = new ProvenanceQuantityContribution(
            new QuantityContributionId("qc-1"),
            "owner",
            Ledger.Route,
            "group",
            new KnownValue(1d, SiUnit.Meter, ["obs-1"], "Q01.1.0"),
            ["obs-1"],
            "Q01.1.0");

        var result = FormulaProvenance.WrapCalculation(
            new KnownValue(1d, SiUnit.Meter, ["obs-1"], "Q01.1.0"),
            "Q01.1.0",
            [new InputReference("project", "p1", "rev-1")],
            ["obs-1"],
            [new QuantityContributionId("qc-1")],
            warnings: ["w1"],
            issues: ["i1"],
            contributions: [contribution]);

        Assert.IsType<CalculationResult<KnownValue>>(result);
        Assert.Equal("Q01.1.0", result.Provenance.MethodVersion);
        Assert.Equal(["w1"], result.Warnings);
        Assert.Equal(["i1"], result.Issues);
        Assert.Single(result.Contributions);
        Assert.Equal("qc-1", result.Contributions[0].Id.Value);
    }

    [Fact]
    public void FormulaProvenance_WrapTaggedCalculation_UsesTaggedMetadata()
    {
        var tagged = new KnownValue(5d, SiUnit.Meter, ["obs:a"], "Q02.1.0");

        var result = FormulaProvenance.WrapTaggedCalculation(
            tagged,
            [new InputReference("project", "p1", "rev-1")],
            [new QuantityContributionId("qc-1")],
            warnings: ["warn"]);

        Assert.Same(tagged, result.Value);
        Assert.Equal("Q02.1.0", result.Provenance.MethodVersion);
        Assert.Equal(tagged.SourceRefs, result.Provenance.SourceRefs);
        Assert.Equal(["warn"], result.Warnings);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void FormulaProvenance_ToCalculationResult_PromotesDerivedResult()
    {
        var derived = new DerivedResult<KnownValue>(
            new KnownValue(3d, SiUnit.Meter, ["obs-1"], "Q01.1.0"),
            CreateProvenance());

        var result = FormulaProvenance.ToCalculationResult(derived, warnings: ["w"], issues: ["i"]);

        Assert.Same(derived.Value, result.Value);
        Assert.Same(derived.Provenance, result.Provenance);
        Assert.Equal(["w"], result.Warnings);
        Assert.Equal(["i"], result.Issues);
    }

    private static ResultProvenance CreateProvenance() =>
        new(
            "Q01.1.0",
            [new InputReference("project", "p1", "rev-1")],
            ["obs-1"],
            [new QuantityContributionId("qc-1")]);
}
