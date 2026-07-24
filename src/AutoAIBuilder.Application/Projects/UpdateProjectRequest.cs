namespace AutoAIBuilder.Application.Projects;

public sealed record UpdateProjectRequest(
    Guid ProjectId,
    string Name,
    string Type,
    int Floors,
    int Units);
