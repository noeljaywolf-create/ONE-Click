namespace OneClick.HttpAdapter.Projects;

public sealed record ProjectViewHttpResponse(
    string ProjectId,
    long Revision,
    string Name,
    IReadOnlyList<string> Capabilities,
    string ScopeDescription,
    string WorldFrame,
    ProjectScopeHttpResponse Scope);

public sealed record ProjectScopeHttpResponse(
    bool HasAcceptedModel,
    IReadOnlyList<string> KnownLimitations,
    int PieceCount,
    int SnapshotCount);
