namespace AutoAIBuilder.Application.History;

public sealed record ActivityLogEntry(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Category,
    string Action,
    string Description,
    ActivityLevel Level,
    Guid? ProjectId = null,
    string? ProjectName = null);
