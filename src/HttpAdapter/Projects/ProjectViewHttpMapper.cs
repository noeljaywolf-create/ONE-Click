using OneClick.Application.Contracts;

namespace OneClick.HttpAdapter.Projects;

public static class ProjectViewHttpMapper
{
    public static ProjectViewHttpResponse FromProjectView(
        ProjectView view,
        string name,
        string scopeDescription,
        string worldFrame = "right_handed_z_up_metres")
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldFrame);

        return new ProjectViewHttpResponse(
            view.ProjectId,
            view.Revision.Value,
            name,
            [.. view.Capabilities.SupportedCommands, .. view.Capabilities.SupportedQueries],
            scopeDescription,
            worldFrame,
            new ProjectScopeHttpResponse(
                view.Scope.HasAcceptedModel,
                view.Scope.KnownLimitations,
                view.Scope.PieceCount,
                view.Scope.SnapshotCount));
    }
}
