namespace AutoAIBuilder.Application.Automation.Execution;

public interface IAutomationAuditRepository
{
    AutomationAuditEntry? Get(Guid id);

    AutomationAuditEntry? GetSuccessfulByIdempotencyKey(string idempotencyKey);

    IReadOnlyList<AutomationAuditEntry> GetRecent(int maximumEntries = 50);

    void Save(AutomationAuditEntry entry);

    int MarkIncompleteAsInterrupted(
        DateTimeOffset interruptedAt,
        string reason);
}
