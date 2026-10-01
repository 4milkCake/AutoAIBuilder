using AutoAIBuilder.Application.Automation.Preview;
using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Application.Automation.Supervised;

public static class SupervisedAutomationProfiles
{
    public const string PreservationTotal = "PRESERVATION_TOTAL_11.6H.1";
}

public enum SupervisedAutomationGateStatus
{
    Passed,
    Blocked,
    Warning
}

public sealed record SupervisedAutomationGate(
    string Code,
    string Name,
    SupervisedAutomationGateStatus Status,
    string Evidence);

public sealed record SupervisedAutomationRequest(
    Guid ProjectId,
    AutomationPreviewPlan PreviewPlan,
    IReadOnlyList<SemanticPoint> Points,
    IReadOnlyList<SemanticComponent> Components,
    string SourceDwgPath,
    string HistoricalMaskPath,
    string HistoricalPointsCsvPath,
    string LegendObjectsCsvPath,
    string AutoLispDirectory,
    string OutputRoot);

public sealed record SupervisedAutomationPreflight(
    Guid ProjectId,
    string PlanId,
    string SourceDwgPath,
    string SourceSha256,
    string HistoricalMaskPath,
    string HistoricalMaskSha256,
    string HistoricalPointsCsvPath,
    string LegendObjectsCsvPath,
    string AutoLispDirectory,
    string OutputRoot,
    int ExpectedPointCount,
    int ExpectedComponentCount,
    int HistoricalPointCount,
    int LegendObjectCount,
    IReadOnlyList<SupervisedAutomationGate> Gates,
    bool CanExecute,
    string SafetyStatement);

public sealed record SupervisedPointComparison(
    string Handle,
    string ExpectedLayer,
    string ActualLayer,
    bool Found,
    bool LayerMatches);

public sealed record SupervisedAutomationMetrics(
    int ExpectedPoints,
    int ResultPoints,
    int CorrectPoints,
    int MissingPoints,
    int WrongLayerPoints,
    double AccuracyPercentage,
    int ExpectedComponents,
    int CorrectComponents,
    int EntitiesBefore,
    int EntitiesAfter,
    int MissingEntityHandles,
    int ProtectedBlocksBefore,
    int ProtectedBlocksAfter,
    int HistoricalPoints,
    int HistoricalMatches);

public sealed record SupervisedAutomationRunResult(
    Guid RunId,
    Guid ProjectId,
    string PlanId,
    string ExecutionProfile,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string RunDirectory,
    string ResultDwgPath,
    string ManifestPath,
    string ProcessLogPath,
    string SourceSha256Before,
    string SourceSha256After,
    string TechnicalCopySha256Before,
    string ResultSha256,
    bool SourceIntegrityConfirmed,
    bool HistoricalReferenceIntegrityConfirmed,
    bool AcceptancePassed,
    SupervisedAutomationMetrics Metrics,
    IReadOnlyList<SupervisedPointComparison> PointComparisons,
    string Summary);

public interface ISupervisedAutomationService
{
    SupervisedAutomationPreflight Inspect(
        SupervisedAutomationRequest request);

    SupervisedAutomationRunResult? LoadLatest(
        Guid projectId,
        string outputRoot);

    Task<SupervisedAutomationRunResult> ExecuteAsync(
        SupervisedAutomationRequest request,
        CancellationToken cancellationToken = default);
}
