namespace AutoAIBuilder.Application.History;

public sealed class ActivityLogService(IActivityLogRepository repository)
{
    public IReadOnlyList<ActivityLogEntry> GetRecent(Guid? projectId = null, int limit = 200)
    {
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        return repository.GetAll()
            .Where(entry => projectId is null || entry.ProjectId == projectId)
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(limit)
            .ToArray();
    }

    public ActivityLogEntry Record(
        string category,
        string action,
        string description,
        ActivityLevel level = ActivityLevel.Information,
        Guid? projectId = null,
        string? projectName = null,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("A categoria do histórico é obrigatória.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("A ação do histórico é obrigatória.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A descrição do histórico é obrigatória.", nameof(description));
        }

        var entry = new ActivityLogEntry(
            Guid.NewGuid(),
            now ?? DateTimeOffset.Now,
            category.Trim(),
            action.Trim(),
            description.Trim(),
            level,
            projectId,
            string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim());

        repository.Append(entry);
        return entry;
    }
}
