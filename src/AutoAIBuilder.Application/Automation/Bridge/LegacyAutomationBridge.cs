using AutoAIBuilder.Application.Automation.Contracts;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Semantics;

namespace AutoAIBuilder.Application.Automation.Bridge;

public enum LegacyRoutineSafety
{
    ReadOnlyAnalysis,
    ReportOutputOnly,
    DrawingMutationBlocked
}

public sealed record LegacyAutomationRoutine(
    string FileName,
    string FullPath,
    string Version,
    string Stage,
    long SizeBytes,
    string Sha256,
    IReadOnlyList<string> Commands,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> Dependencies,
    string ContractId,
    LegacyRoutineSafety Safety,
    string SafetyReason,
    bool IsPreferredVersion);

public sealed record LegacyAutomationContractLink(
    AutomationMaskDefinition Contract,
    IReadOnlyList<string> SourceFiles,
    string MappingSummary,
    bool ApplyEnabled = false);

public sealed record LegacyAutomationSimulation(
    bool HasSemanticDataset,
    int PointCount,
    int ElectricalPointCount,
    int HydraulicPointCount,
    int ComponentCount,
    int SemanticLayerCount,
    int DirectionCount,
    int PendingReviewCount,
    IReadOnlyList<AutomationPlanAction> Actions,
    IReadOnlyList<string> Warnings,
    string Summary);

public sealed record LegacyAutomationBridgeSnapshot(
    string SourceDirectory,
    DateTimeOffset AnalyzedAt,
    IReadOnlyList<LegacyAutomationRoutine> Routines,
    IReadOnlyList<LegacyAutomationContractLink> ContractLinks,
    LegacyAutomationSimulation Simulation,
    bool SourceFilesUnchanged,
    string SafetyStatement);

public interface ILegacyAutomationBridgeService
{
    LegacyAutomationBridgeSnapshot Analyze(
        string sourceDirectory,
        SemanticWorkspaceSnapshot semanticSnapshot);
}
