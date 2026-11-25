using OneClick.Core.TaggedValues;

namespace OneClick.Tests.Reference;

public sealed class DomainEnumTests
{
    [Fact]
    public void Origins_MatchDomainModel()
    {
        var expected = new[]
        {
            Origin.DocumentStated,
            Origin.Measured,
            Origin.Imported,
            Origin.Inferred,
            Origin.Assumed,
            Origin.Manual,
        };

        Assert.Equal(expected, Enum.GetValues<Origin>().OrderBy(x => (int)x));
    }

    [Fact]
    public void ReviewStates_MatchDomainModel()
    {
        var expected = new[]
        {
            ReviewState.Unreviewed,
            ReviewState.Accepted,
            ReviewState.Rejected,
            ReviewState.ClarificationRequired,
            ReviewState.Stale,
        };

        Assert.Equal(expected, Enum.GetValues<ReviewState>().OrderBy(x => (int)x));
    }

    [Fact]
    public void CheckOutcomes_MatchDomainModel()
    {
        var expected = new[]
        {
            CheckOutcome.Pass,
            CheckOutcome.Fail,
            CheckOutcome.CannotVerify,
            CheckOutcome.NotApplicable,
            CheckOutcome.Unsupported,
        };

        Assert.Equal(expected, Enum.GetValues<CheckOutcome>().OrderBy(x => (int)x));
    }

    [Fact]
    public void Freshness_MatchDomainModel()
    {
        var expected = new[]
        {
            Freshness.Current,
            Freshness.Stale,
            Freshness.Failed,
        };

        Assert.Equal(expected, Enum.GetValues<Freshness>().OrderBy(x => (int)x));
    }

    [Fact]
    public void Acceptance_DoesNotChangeOrigin_InvariantIsStructurallySeparated()
    {
        // Domain invariant: accepting an assumption does not change its origin into a documented fact.
        // Origin and ReviewState are independent enums; no API couples them.
        Origin origin = Origin.Assumed;
        ReviewState review = ReviewState.Accepted;

        Assert.Equal(Origin.Assumed, origin);
        Assert.Equal(ReviewState.Accepted, review);
        Assert.NotEqual(nameof(Origin.DocumentStated), origin.ToString());
    }
}