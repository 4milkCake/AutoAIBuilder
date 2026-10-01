using AutoAIBuilder.Application.CadVisualization;
using AutoAIBuilder.Application.Semantics;

namespace AutoAIBuilder.Application.Automation.Preview;

public enum AutomationPreviewCategory
{
    Original,
    Detected,
    Proposed,
    Corrected,
    Ignored
}

public enum AutomationPreviewDecisionStatus
{
    Pending,
    Approved,
    Rejected,
    Protected
}

public sealed record AutomationPreviewDecision(
    Guid Id,
    string PlanId,
    Guid ProjectId,
    Guid DatasetId,
    string GroupId,
    AutomationPreviewDecisionStatus Status,
    string? Note,
    DateTimeOffset DecidedAt);

public sealed record AutomationPreviewGroup(
    string Id,
    AutomationPreviewCategory Category,
    string Name,
    string Description,
    string Source,
    string IntendedEffect,
    int ItemCount,
    bool WritesDuringFutureApply,
    bool RequiresDecision,
    AutomationPreviewDecisionStatus Decision,
    string? DecisionNote,
    DateTimeOffset? DecidedAt,
    IReadOnlyList<Guid> PointIds,
    IReadOnlyList<Guid> ComponentIds);

public sealed record AutomationPreviewPlan(
    string Id,
    Guid ProjectId,
    Guid DatasetId,
    string SourceDwgPath,
    string SourceSha256,
    string DatasetFingerprint,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AutomationPreviewGroup> Groups,
    bool SourceIntegrityConfirmed,
    int RequiredDecisionCount,
    int ApprovedCount,
    int RejectedCount,
    int PendingCount,
    bool IsReadyForSupervisedExecution,
    string BeforeSummary,
    string AfterSummary,
    string SafetyStatement);

public interface IAutomationPreviewDecisionRepository
{
    IReadOnlyList<AutomationPreviewDecision> GetForPlan(string planId);

    void Save(AutomationPreviewDecision decision);
}

public interface IAutomationPreviewService
{
    AutomationPreviewPlan Build(
        Guid projectId,
        SemanticWorkspaceSnapshot semanticSnapshot,
        CadVisualizationSnapshot? cadSnapshot);

    AutomationPreviewPlan Decide(
        AutomationPreviewPlan currentPlan,
        string groupId,
        AutomationPreviewDecisionStatus status,
        string? note);
}
