namespace AutoAIBuilder.Application.History;

public interface IActivityLogRepository
{
    IReadOnlyList<ActivityLogEntry> GetAll();

    void Append(ActivityLogEntry entry);
}
