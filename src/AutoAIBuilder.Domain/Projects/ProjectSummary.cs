namespace AutoAIBuilder.Domain.Projects;

public sealed record ProjectSummary(
    Guid Id,
    string Name,
    string Type,
    int Floors,
    int Units,
    IReadOnlyList<string> Disciplines,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
