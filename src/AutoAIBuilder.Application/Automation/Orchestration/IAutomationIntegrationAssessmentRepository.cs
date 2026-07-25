namespace AutoAIBuilder.Application.Automation.Orchestration;

public interface IAutomationIntegrationAssessmentRepository
{
    IReadOnlyList<AutomationIntegrationAssessment> GetRecent(
        int maximumEntries = 50);

    void Add(AutomationIntegrationAssessment assessment);
}
