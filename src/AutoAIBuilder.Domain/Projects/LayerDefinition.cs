namespace AutoAIBuilder.Domain.Projects;

public sealed record LayerDefinition(
    string Key,
    string Name,
    string Discipline,
    string Color,
    bool IsVisible = true);
