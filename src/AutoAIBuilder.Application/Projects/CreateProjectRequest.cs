namespace AutoAIBuilder.Application.Projects;

public sealed record CreateProjectRequest(
    string Name,
    string Type,
    int Floors,
    int Units);
