using OneClick.Application.Contracts;
using OneClick.HttpAdapter.Projects;

namespace OneClick.Tests.Integration;

public sealed class ProjectViewHttpMapperTests
{
    [Fact]
    public void Maps_ProjectView_ScopeCounts_And_CoreFields()
    {
        var view = new ProjectView(
            "proj-1",
            new ProjectRevision(7),
            new ProjectCapabilities(
                ["ImportDocument", "UpsertRoute"],
                ["QueryProject"]),
            new ProjectScope(true, ["No catalogue mapping loaded."])
            {
                PieceCount = 2,
                SnapshotCount = 1,
            });

        var response = ProjectViewHttpMapper.FromProjectView(
            view,
            "Demo project",
            "First-slice demo scope");

        Assert.Equal("proj-1", response.ProjectId);
        Assert.Equal(7, response.Revision);
        Assert.Equal("Demo project", response.Name);
        Assert.Equal("First-slice demo scope", response.ScopeDescription);
        Assert.Equal("right_handed_z_up_metres", response.WorldFrame);
        Assert.Equal(["ImportDocument", "UpsertRoute", "QueryProject"], response.Capabilities);
        Assert.True(response.Scope.HasAcceptedModel);
        Assert.Equal(["No catalogue mapping loaded."], response.Scope.KnownLimitations);
        Assert.Equal(2, response.Scope.PieceCount);
        Assert.Equal(1, response.Scope.SnapshotCount);
    }

    [Fact]
    public void Maps_Zero_ScopeCounts_When_Model_Is_Empty()
    {
        var view = new ProjectView(
            "proj-empty",
            ProjectRevision.Initial,
            new ProjectCapabilities([], []),
            new ProjectScope(false, [])
            {
                PieceCount = 0,
                SnapshotCount = 0,
            });

        var response = ProjectViewHttpMapper.FromProjectView(
            view,
            "Empty project",
            "Empty scope");

        Assert.Equal(0, response.Scope.PieceCount);
        Assert.Equal(0, response.Scope.SnapshotCount);
        Assert.False(response.Scope.HasAcceptedModel);
        Assert.Empty(response.Scope.KnownLimitations);
    }
}
