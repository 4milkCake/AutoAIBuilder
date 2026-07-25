namespace AutoAIBuilder.Application.Automation.Execution;

public enum AutomationAuditStatus
{
    Planned,
    Simulated,
    Running,
    Succeeded,
    Reused,
    Rejected,
    FailedRolledBack,
    CancelledRolledBack,
    Interrupted
}
