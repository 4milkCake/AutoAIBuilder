using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using AutoAIBuilder.Application.Automation.Bridge;
using AutoAIBuilder.Application.Automation.Execution;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string _autoLispDirectory = FindDefaultAutoLispDirectory();
    private string _automationBridgeStatus = "AGUARDANDO ANÁLISE";
    private string _automationBridgeStatusAccent = "#627087";
    private string _automationBridgeSummary =
        "Selecione a pasta histórica de rotinas AutoLISP.";
    private string _automationBridgeAnalyzedAt = "Ainda não analisado";
    private string _automationBridgeSafety =
        "Nenhuma rotina será carregada ou executada.";
    private bool _automationBridgeSourceUnchanged;
    private int _automationBridgePointCount;
    private int _automationBridgeElectricalCount;
    private int _automationBridgeHydraulicCount;
    private int _automationBridgeComponentCount;
    private int _automationBridgeLayerCount;
    private int _automationBridgeDirectionCount;
    private int _automationBridgePendingCount;

    public ObservableCollection<LegacyRoutineItemViewModel>
        AutomationBridgeRoutines { get; } = [];

    public ObservableCollection<LegacyContractLinkItemViewModel>
        AutomationBridgeContracts { get; } = [];

    public ObservableCollection<LegacySimulationActionItemViewModel>
        AutomationBridgeActions { get; } = [];

    public ObservableCollection<string> AutomationBridgeWarnings { get; } = [];

    public ICommand ChooseAutoLispDirectoryCommand { get; }

    public ICommand AnalyzeAutomationBridgeCommand { get; }

    public string AutoLispDirectory
    {
        get => _autoLispDirectory;
        private set
        {
            if (SetField(ref _autoLispDirectory, value))
            {
                OnPropertyChanged(nameof(AutoLispDirectoryDisplay));
            }
        }
    }

    public string AutoLispDirectoryDisplay =>
        string.IsNullOrWhiteSpace(AutoLispDirectory)
            ? "Pasta não selecionada"
            : AutoLispDirectory;

    public string AutomationBridgeStatus
    {
        get => _automationBridgeStatus;
        private set => SetField(ref _automationBridgeStatus, value);
    }

    public string AutomationBridgeStatusAccent
    {
        get => _automationBridgeStatusAccent;
        private set => SetField(ref _automationBridgeStatusAccent, value);
    }

    public string AutomationBridgeSummary
    {
        get => _automationBridgeSummary;
        private set => SetField(ref _automationBridgeSummary, value);
    }

    public string AutomationBridgeAnalyzedAt
    {
        get => _automationBridgeAnalyzedAt;
        private set => SetField(ref _automationBridgeAnalyzedAt, value);
    }

    public string AutomationBridgeSafety
    {
        get => _automationBridgeSafety;
        private set => SetField(ref _automationBridgeSafety, value);
    }

    public bool AutomationBridgeSourceUnchanged
    {
        get => _automationBridgeSourceUnchanged;
        private set
        {
            if (SetField(ref _automationBridgeSourceUnchanged, value))
            {
                OnPropertyChanged(nameof(AutomationBridgeIntegrityText));
                OnPropertyChanged(nameof(AutomationBridgeIntegrityAccent));
            }
        }
    }

    public string AutomationBridgeIntegrityText =>
        AutomationBridgeSourceUnchanged
            ? "FONTES INTACTAS"
            : "INTEGRIDADE NÃO CONFIRMADA";

    public string AutomationBridgeIntegrityAccent =>
        AutomationBridgeSourceUnchanged ? "#36D17C" : "#F8C33A";

    public int AutomationBridgeRoutineCount => AutomationBridgeRoutines.Count;

    public int AutomationBridgeBlockedCount =>
        AutomationBridgeRoutines.Count(routine => routine.IsBlocked);

    public int AutomationBridgePointCount
    {
        get => _automationBridgePointCount;
        private set => SetField(ref _automationBridgePointCount, value);
    }

    public int AutomationBridgeElectricalCount
    {
        get => _automationBridgeElectricalCount;
        private set => SetField(ref _automationBridgeElectricalCount, value);
    }

    public int AutomationBridgeHydraulicCount
    {
        get => _automationBridgeHydraulicCount;
        private set => SetField(ref _automationBridgeHydraulicCount, value);
    }

    public int AutomationBridgeComponentCount
    {
        get => _automationBridgeComponentCount;
        private set => SetField(ref _automationBridgeComponentCount, value);
    }

    public int AutomationBridgeLayerCount
    {
        get => _automationBridgeLayerCount;
        private set => SetField(ref _automationBridgeLayerCount, value);
    }

    public int AutomationBridgeDirectionCount
    {
        get => _automationBridgeDirectionCount;
        private set => SetField(ref _automationBridgeDirectionCount, value);
    }

    public int AutomationBridgePendingCount
    {
        get => _automationBridgePendingCount;
        private set => SetField(ref _automationBridgePendingCount, value);
    }

    private void ChooseAutoLispDirectory()
    {
        var selected = _filePicker.PickAutoLispDirectory(AutoLispDirectory);
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        AutoLispDirectory = selected;
        RefreshAutomationBridge();
    }

    private void RefreshAutomationBridge()
    {
        if (string.IsNullOrWhiteSpace(AutoLispDirectory)
            || !Directory.Exists(AutoLispDirectory))
        {
            AutomationBridgeStatus = "PASTA NÃO ENCONTRADA";
            AutomationBridgeStatusAccent = "#F8C33A";
            AutomationBridgeSummary =
                "Selecione a pasta que contém as rotinas históricas .lsp.";
            return;
        }

        try
        {
            var snapshot = _legacyAutomationBridgeService.Analyze(
                AutoLispDirectory,
                _semanticSnapshot);
            ReplaceItems(
                AutomationBridgeRoutines,
                snapshot.Routines.Select(LegacyRoutineItemViewModel.From));
            ReplaceItems(
                AutomationBridgeContracts,
                snapshot.ContractLinks.Select(
                    LegacyContractLinkItemViewModel.From));
            ReplaceItems(
                AutomationBridgeActions,
                snapshot.Simulation.Actions.Select(
                    LegacySimulationActionItemViewModel.From));
            ReplaceItems(
                AutomationBridgeWarnings,
                snapshot.Simulation.Warnings);

            AutomationBridgeSourceUnchanged = snapshot.SourceFilesUnchanged;
            AutomationBridgeSafety = snapshot.SafetyStatement;
            AutomationBridgeAnalyzedAt =
                snapshot.AnalyzedAt.ToString("dd/MM/yyyy HH:mm:ss");
            AutomationBridgeSummary = snapshot.Simulation.Summary;
            AutomationBridgePointCount = snapshot.Simulation.PointCount;
            AutomationBridgeElectricalCount =
                snapshot.Simulation.ElectricalPointCount;
            AutomationBridgeHydraulicCount =
                snapshot.Simulation.HydraulicPointCount;
            AutomationBridgeComponentCount =
                snapshot.Simulation.ComponentCount;
            AutomationBridgeLayerCount =
                snapshot.Simulation.SemanticLayerCount;
            AutomationBridgeDirectionCount =
                snapshot.Simulation.DirectionCount;
            AutomationBridgePendingCount =
                snapshot.Simulation.PendingReviewCount;
            AutomationBridgeStatus = snapshot.SourceFilesUnchanged
                ? "SIMULAÇÃO CONCLUÍDA"
                : "REVISAR INTEGRIDADE";
            AutomationBridgeStatusAccent = snapshot.SourceFilesUnchanged
                ? "#36D17C"
                : "#F8C33A";

            OnPropertyChanged(nameof(AutomationBridgeRoutineCount));
            OnPropertyChanged(nameof(AutomationBridgeBlockedCount));
            StatusMessage =
                "Ponte 11.6F atualizada sem executar AutoLISP ou alterar DWGs.";
        }
        catch (Exception exception)
        {
            AutomationBridgeStatus = "ANÁLISE INTERROMPIDA";
            AutomationBridgeStatusAccent = "#FF5D68";
            AutomationBridgeSummary = exception.Message;
            AutomationBridgeSafety =
                "Nenhuma rotina foi executada durante a falha de análise.";
            AutomationBridgeSourceUnchanged = false;
            TryWriteDiagnostic(
                AutoAIBuilder.Application.Diagnostics.DiagnosticLevel.Error,
                "AutomationBridge",
                "Falha na análise estática das rotinas AutoLISP.",
                exception);
        }
    }

    private static string FindDefaultAutoLispDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "Automacao_CAD",
                "04_Scripts"),
            @"C:\Users\danil\OneDrive\Documentos\Automacao_CAD\04_Scripts"
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }
}

public sealed record LegacyRoutineItemViewModel(
    string FileName,
    string Version,
    string Stage,
    string Commands,
    string Inputs,
    string Outputs,
    string Dependencies,
    string ContractId,
    string Safety,
    string SafetyReason,
    string SafetyAccent,
    string Sha256,
    string Size,
    bool IsBlocked,
    bool IsPreferred)
{
    public static LegacyRoutineItemViewModel From(
        LegacyAutomationRoutine routine) =>
        new(
            routine.FileName,
            $"v{routine.Version}",
            routine.Stage,
            routine.Commands.Count == 0
                ? "Nenhum comando c: encontrado"
                : string.Join(" • ", routine.Commands),
            string.Join(" • ", routine.Inputs),
            string.Join(" • ", routine.Outputs),
            routine.Dependencies.Count == 0
                ? "Etapa inicial"
                : string.Join(" → ", routine.Dependencies),
            routine.ContractId,
            routine.Safety switch
            {
                LegacyRoutineSafety.DrawingMutationBlocked => "ALTERAÇÃO BLOQUEADA",
                LegacyRoutineSafety.ReportOutputOnly => "SÓ RELATÓRIOS",
                _ => "SOMENTE LEITURA"
            },
            routine.SafetyReason,
            routine.Safety == LegacyRoutineSafety.DrawingMutationBlocked
                ? "#F8C33A"
                : "#36D17C",
            routine.Sha256,
            $"{routine.SizeBytes / 1024d:0.0} KiB",
            routine.Safety == LegacyRoutineSafety.DrawingMutationBlocked,
            routine.IsPreferredVersion);
}

public sealed record LegacyContractLinkItemViewModel(
    string Name,
    string IdVersion,
    string Discipline,
    string SourceFiles,
    string MappingSummary,
    string ApplyStatus)
{
    public static LegacyContractLinkItemViewModel From(
        LegacyAutomationContractLink link) =>
        new(
            link.Contract.Name,
            $"{link.Contract.Id} @ {link.Contract.Version}",
            link.Contract.Discipline,
            string.Join(" • ", link.SourceFiles),
            link.MappingSummary,
            link.ApplyEnabled ? "APLICAÇÃO HABILITADA" : "APLICAÇÃO BLOQUEADA");
}

public sealed record LegacySimulationActionItemViewModel(
    int Order,
    string Code,
    string Description,
    string Mode,
    string Accent)
{
    public static LegacySimulationActionItemViewModel From(
        AutomationPlanAction action) =>
        new(
            action.Order,
            action.Code,
            action.Description,
            action.WritesDuringApply
                ? "PROPOSTA — NÃO APLICADA"
                : "ANÁLISE",
            action.WritesDuringApply ? "#F8C33A" : "#36D17C");
}
