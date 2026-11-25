using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Tests.Reference;

public sealed class TaggedValueTests
{
    [Fact]
    public void KnownValue_CarriesRequiredFields()
    {
        var value = new KnownValue(1.25, SiUnit.Meter, ["obs-1"], "m-1.0");

        Assert.Equal(1.25, value.Value);
        Assert.Equal(SiUnit.Meter, value.Unit);
        Assert.Equal(["obs-1"], value.SourceRefs);
        Assert.Equal("m-1.0", value.MethodVersion);
    }

    [Fact]
    public void ProvisionalValue_CarriesAssumptionAndIssueRefs()
    {
        var value = new ProvisionalValue(
            3.0,
            SiUnit.Meter,
            ["obs-2"],
            "m-1.0",
            ["assume-1"],
            ["issue-7"]);

        Assert.Equal(3.0, value.Value);
        Assert.Equal(["assume-1"], value.AssumptionRefs);
        Assert.Equal(["issue-7"], value.IssueRefs);
    }

    [Fact]
    public void UnknownValue_HasNullValue_Reason_AndIssueRefs()
    {
        var value = new UnknownValue(
            SiUnit.Meter,
            ["obs-3"],
            "m-1.0",
            "Elevation missing from source",
            ["issue-9"]);

        Assert.Null(value.Value);
        Assert.Equal("Elevation missing from source", value.Reason);
        Assert.Equal(["issue-9"], value.IssueRefs);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void KnownValue_RejectsNonFiniteNumbers(double invalid)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KnownValue(invalid, SiUnit.Meter, ["obs"], "m-1.0"));

        Assert.Contains("finite", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ProvisionalValue_RejectsNonFiniteNumbers(double invalid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProvisionalValue(invalid, SiUnit.Meter, ["obs"], "m-1.0", ["a"], ["i"]));
    }

    [Fact]
    public void TaggedValues_RequireMethodVersionAndSourceRefs()
    {
        Assert.Throws<ArgumentException>(() => new KnownValue(1, SiUnit.Meter, ["obs"], ""));
        Assert.Throws<ArgumentNullException>(() => new KnownValue(1, SiUnit.Meter, null!, "m-1.0"));
        Assert.Throws<ArgumentException>(() =>
            new UnknownValue(SiUnit.Meter, ["obs"], "m-1.0", "", ["issue"]));
    }

    [Fact]
    public void SourceRefs_AreCopiedAndValidated()
    {
        var refs = new List<string> { "obs-1" };
        var value = new KnownValue(1, SiUnit.Meter, refs, "m-1.0");
        refs[0] = "mutated";

        Assert.Equal(["obs-1"], value.SourceRefs);
        Assert.Throws<ArgumentException>(() => new KnownValue(1, SiUnit.Meter, [" "], "m-1.0"));
    }

    [Fact]
    public void UnknownValue_IsNotRepresentedAsZeroOrNaN()
    {
        TaggedValue value = new UnknownValue(SiUnit.Meter, [], "m-1.0", "Missing", ["issue-1"]);

        var unknown = Assert.IsType<UnknownValue>(value);
        Assert.Null(unknown.Value);
        Assert.NotEqual(0d, unknown.Value ?? double.MinValue);
    }
}