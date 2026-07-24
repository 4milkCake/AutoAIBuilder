using AutoAIBuilder.Domain.Automation;
using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Application.Dashboard;

public sealed record DashboardSnapshot(
    ProjectSummary Project,
    IReadOnlyList<WorkflowStep> Workflow,
    IReadOnlyList<DashboardAgent> Agents,
    IReadOnlyList<DashboardMetric> Metrics,
    IReadOnlyList<DashboardActivity> Activities,
    string RecommendedAction);

public sealed record DashboardAgent(
    string Name,
    string Role,
    WorkflowState State,
    int? Progress);

public sealed record DashboardMetric(
    string Label,
    string Value,
    int Progress,
    string Accent);

public sealed record DashboardActivity(
    DateTimeOffset OccurredAt,
    string Description,
    string Accent);
