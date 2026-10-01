using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AutoAIBuilder.Application.CadVisualization;
using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Recognition;
using AutoAIBuilder.Domain.Recognition;
using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly List<RecognitionCandidateItemViewModel>
        _allRecognitionCandidates = [];
    private RecognitionSession? _recognitionSession;
    private CadVisualizationSnapshot? _recognitionCadSnapshot;
    private RecognitionCandidateItemViewModel? _selectedRecognitionCandidate;
    private string _recognitionSourcePath = string.Empty;
    private string _selectedRecognitionFilter = "Todos";
    private string _recognitionStage = "AGUARDANDO NOVO DWG";
    private string _recognitionStageAccent = "#627087";
    private string _recognitionSummary =
        "Selecione um DWG que ainda não tenha sido analisado.";
    private string _recognitionEditorDiscipline = "Elétrico";
    private string _recognitionEditorCode = string.Empty;
    private string _recognitionEditorDescription = string.Empty;
    private string _recognitionEditorLayer = string.Empty;
    private string _recognitionEditorHeight = string.Empty;
    private string _recognitionEditorNote = string.Empty;
    private bool _isRecognitionRunning;

    public ObservableCollection<RecognitionCandidateItemViewModel>
        RecognitionCandidates { get; } = [];

    public ObservableCollection<CadOverlayPointViewModel>
        RecognitionOverlayPoints { get; } = [];

    public ObservableCollection<CadLayerItemViewModel>
        RecognitionCadLayers { get; } = [];

    public ObservableCollection<RecognitionLegendEntryItemViewModel>
        RecognitionLegendEntries { get; } = [];

    public IReadOnlyList<string> RecognitionFilterOptions { get; } =
    [
        "Todos",
        "Alta confiança",
        "Identificados pela legenda",
        "Revisão obrigatória",
        "Não classificados",
        "Pendentes",
        "Aprovados",
        "Corrigidos",
        "Rejeitados"
    ];

    public IReadOnlyList<string> RecognitionDisciplineOptions { get; } =
    [
        "Elétrico",
        "Hidráulico"
    ];

    public ICommand ChooseRecognitionDwgCommand { get; }

    public ICommand AnalyzeRecognitionCommand { get; }

    public ICommand ApproveRecognitionCandidateCommand { get; }

    public ICommand RejectRecognitionCandidateCommand { get; }

    public ICommand SaveRecognitionCorrectionCommand { get; }

    public ICommand OpenRecognitionRunCommand { get; }

    public ICommand SelectRecognitionOverlayPointCommand { get; private set; } =
        null!;

    public string RecognitionSourcePath
    {
        get => _recognitionSourcePath;
        private set
        {
            if (SetField(ref _recognitionSourcePath, value))
            {
                RaiseRecognitionCommandStates();
            }
        }
    }

    public string SelectedRecognitionFilter
    {
        get => _selectedRecognitionFilter;
        set
        {
            if (SetField(ref _selectedRecognitionFilter, value))
            {
                ApplyRecognitionFilter();
            }
        }
    }

    public RecognitionCandidateItemViewModel? SelectedRecognitionCandidate
    {
        get => _selectedRecognitionCandidate;
        set
        {
            if (!SetField(ref _selectedRecognitionCandidate, value))
            {
                return;
            }

            LoadRecognitionEditor(value);
            OnPropertyChanged(nameof(SelectedRecognitionPointId));
            RaiseRecognitionCommandStates();
        }
    }

    public Guid? SelectedRecognitionPointId =>
        SelectedRecognitionCandidate?.Id;

    public CadVisualizationSnapshot? RecognitionCadSnapshot
    {
        get => _recognitionCadSnapshot;
        private set => SetField(ref _recognitionCadSnapshot, value);
    }

    public string RecognitionStage
    {
        get => _recognitionStage;
        private set => SetField(ref _recognitionStage, value);
    }

    public string RecognitionStageAccent
    {
        get => _recognitionStageAccent;
        private set => SetField(ref _recognitionStageAccent, value);
    }

    public string RecognitionSummary
    {
        get => _recognitionSummary;
        private set => SetField(ref _recognitionSummary, value);
    }

    public bool IsRecognitionRunning
    {
        get => _isRecognitionRunning;
        private set
        {
            if (SetField(ref _isRecognitionRunning, value))
            {
                OnPropertyChanged(nameof(RecognitionAnalyzeButtonText));
                OnPropertyChanged(nameof(RecognitionRunningVisibility));
                RaiseRecognitionCommandStates();
            }
        }
    }

    public Visibility RecognitionRunningVisibility =>
        IsRecognitionRunning ? Visibility.Visible : Visibility.Collapsed;

    public string RecognitionAnalyzeButtonText =>
        IsRecognitionRunning
            ? "Lendo cópia técnica…"
            : "Analisar sem alterar o DWG";

    public int RecognitionCandidateCount =>
        _recognitionSession?.Candidates.Count ?? 0;

    public int RecognitionHighConfidenceCount =>
        _recognitionSession?.Candidates.Count(candidate =>
            candidate.ConfidenceScore >= 85) ?? 0;

    public int RecognitionNeedsReviewCount =>
        _recognitionSession?.Candidates.Count(candidate =>
            candidate.ConfidenceScore < 85
            && candidate.Status == RecognitionCandidateStatus.Pending) ?? 0;

    public int RecognitionReviewedCount =>
        _recognitionSession?.Candidates.Count(candidate =>
            candidate.Status != RecognitionCandidateStatus.Pending) ?? 0;

    public string RecognitionInventorySummary =>
        _recognitionSession is null
            ? "Inventário ainda não gerado"
            : $"{_recognitionSession.InventoryEntityCount} entidades • "
              + $"{_recognitionSession.InventoryInsertCount} blocos INSERT • "
              + $"{_recognitionSession.InventoryExpandedInsertCount} internos • "
              + "zero comandos de mutação";

    public string RecognitionIntegrity =>
        _recognitionSession?.OriginalIntegrityConfirmed == true
            ? "ORIGINAL ÍNTEGRO — SHA-256 CONFIRMADO"
            : "Aguardando verificação";

    public string RecognitionProfileSource =>
        _recognitionSession?.ProfileSource
        ?? "A base semântica validada do projeto será usada como referência.";

    public string RecognitionRunDirectory =>
        _recognitionSession?.RunDirectory ?? string.Empty;

    public string RecognitionLegendStatus =>
        _recognitionSession?.LegendAnalysis?.Status
        ?? "A legenda será procurada durante a próxima análise.";

    public string RecognitionLegendAccent =>
        _recognitionSession?.LegendAnalysis?.Entries.Count > 0
            ? "#36D17C"
            : _recognitionSession?.LegendAnalysis?.IsDetected == true
                ? "#F8C33A"
                : "#627087";

    public string RecognitionLegendSummary
    {
        get
        {
            var legend = _recognitionSession?.LegendAnalysis;
            if (legend is null || !legend.IsDetected)
            {
                return "Nenhum dicionário local disponível.";
            }

            return $"{legend.AnchorCount} cabeçalho(s) • "
                   + $"{legend.Entries.Count} par(es) confirmado(s) • "
                   + $"{legend.UnpairedDescriptionCount} descrição(ões) "
                   + "ainda sem símbolo pareado.";
        }
    }

    public string RecognitionEditorDiscipline
    {
        get => _recognitionEditorDiscipline;
        set => SetField(ref _recognitionEditorDiscipline, value);
    }

    public string RecognitionEditorCode
    {
        get => _recognitionEditorCode;
        set => SetField(ref _recognitionEditorCode, value);
    }

    public string RecognitionEditorDescription
    {
        get => _recognitionEditorDescription;
        set => SetField(ref _recognitionEditorDescription, value);
    }

    public string RecognitionEditorLayer
    {
        get => _recognitionEditorLayer;
        set => SetField(ref _recognitionEditorLayer, value);
    }

    public string RecognitionEditorHeight
    {
        get => _recognitionEditorHeight;
        set => SetField(ref _recognitionEditorHeight, value);
    }

    public string RecognitionEditorNote
    {
        get => _recognitionEditorNote;
        set => SetField(ref _recognitionEditorNote, value);
    }

    private void InitializeRecognitionCommands()
    {
        SelectRecognitionOverlayPointCommand =
            new RelayCommand<CadOverlayPointViewModel>(
                point =>
                {
                    if (point is null)
                    {
                        return;
                    }

                    var candidate =
                        _allRecognitionCandidates.FirstOrDefault(
                            item => item.Id == point.PointId);
                    if (candidate is null)
                    {
                        return;
                    }

                    if (!RecognitionCandidates.Contains(candidate))
                    {
                        SelectedRecognitionFilter = "Todos";
                    }

                    SelectedRecognitionCandidate = candidate;
                });
    }

    private void RefreshRecognition()
    {
        InitializeRecognitionCommandsIfRequired();
        if (SelectedProject is null)
        {
            ClearRecognition(
                "Selecione um projeto antes de reconhecer um DWG.");
            return;
        }

        var session = _cadRecognitionService.LoadLatest(SelectedProject.Id);
        if (session is null)
        {
            ClearRecognition(
                "Selecione um DWG que ainda não tenha sido analisado.");
            return;
        }

        ApplyRecognitionSession(session);
        RecognitionStage = "ÚLTIMA ANÁLISE RESTAURADA";
        RecognitionStageAccent = "#36D17C";
        RecognitionSourcePath = session.SourceDwgPath;
        RecognitionCadSnapshot = _cadVisualizationService.TryLoadCached(
            SelectedProject.Id,
            session.SourceDwgPath);
        ApplyRecognitionCadLayers();
    }

    private void InitializeRecognitionCommandsIfRequired()
    {
        if (SelectRecognitionOverlayPointCommand is null)
        {
            InitializeRecognitionCommands();
        }
    }

    private void ChooseRecognitionDwg()
    {
        var selected = _filePicker.PickRecognitionDwg(RecognitionSourcePath);
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        RecognitionSourcePath = selected;
        RecognitionStage = "DWG SELECIONADO";
        RecognitionStageAccent = "#2C9BFF";
        RecognitionSummary =
            "Pronto para inventariar uma cópia técnica somente leitura.";
    }

    private bool CanAnalyzeRecognition() =>
        SelectedProject is not null
        && !IsRecognitionRunning
        && !string.IsNullOrWhiteSpace(RecognitionSourcePath);

    private async Task AnalyzeRecognitionAsync(
        CancellationToken cancellationToken)
    {
        if (SelectedProject is null)
        {
            return;
        }

        InitializeRecognitionCommandsIfRequired();
        IsRecognitionRunning = true;
        RecognitionStage = "AUTOCAD EM LEITURA";
        RecognitionStageAccent = "#2C9BFF";
        RecognitionSummary =
            "Inventariando handles, blocos, layers e posições na cópia técnica.";
        try
        {
            var reference = _semanticWorkspaceService.GetForProject(
                SelectedProject.Id);
            var session = await _cadRecognitionService.AnalyzeAsync(
                SelectedProject.Id,
                RecognitionSourcePath,
                reference,
                cancellationToken);
            ApplyRecognitionSession(session);

            try
            {
                RecognitionCadSnapshot =
                    await _cadVisualizationService.GenerateAsync(
                        SelectedProject.Id,
                        RecognitionSourcePath,
                        cancellationToken);
                ApplyRecognitionCadLayers();
            }
            catch (Exception exception) when (
                exception is IOException
                    or InvalidDataException
                    or InvalidOperationException
                    or UnauthorizedAccessException)
            {
                RecognitionCadSnapshot = null;
                RecognitionSummary +=
                    $" A planta não pôde ser renderizada: {exception.Message}";
            }

            RecognitionStage = "CANDIDATOS PRONTOS PARA REVISÃO";
            RecognitionStageAccent = "#36D17C";
            StatusMessage =
                $"11.6I encontrou {session.Candidates.Count} candidato(s); "
                + "nenhuma alteração foi feita no DWG.";
            TryRecordActivity(
                "Análise semântica",
                "Reconhecimento supervisionado 11.6I",
                $"{session.Candidates.Count} candidatos em "
                + $"{session.InventoryEntityCount} entidades; original íntegro.",
                ActivityLevel.Information,
                SelectedProject);
        }
        finally
        {
            IsRecognitionRunning = false;
        }
    }

    private void ReviewRecognitionCandidate(
        RecognitionCandidateItemViewModel? item,
        RecognitionCandidateStatus status)
    {
        if (SelectedProject is null
            || _recognitionSession is null
            || item is null)
        {
            return;
        }

        var selectedId = item.Id;
        var session = _cadRecognitionService.Review(
            SelectedProject.Id,
            _recognitionSession.Id,
            selectedId,
            status,
            note: RecognitionEditorNote);
        ApplyRecognitionSession(session, selectedId);
        StatusMessage = status == RecognitionCandidateStatus.Approved
            ? "Candidato aprovado e registrado na auditoria."
            : "Candidato rejeitado; o DWG não foi alterado.";
    }

    private void SaveRecognitionCorrection()
    {
        if (SelectedProject is null
            || _recognitionSession is null
            || SelectedRecognitionCandidate is null)
        {
            return;
        }

        var discipline = RecognitionEditorDiscipline == "Hidráulico"
            ? SemanticDiscipline.Hydraulic
            : SemanticDiscipline.Electrical;
        var selectedId = SelectedRecognitionCandidate.Id;
        var session = _cadRecognitionService.Review(
            SelectedProject.Id,
            _recognitionSession.Id,
            selectedId,
            RecognitionCandidateStatus.Corrected,
            new RecognitionCorrectionRequest(
                discipline,
                RecognitionEditorCode,
                RecognitionEditorDescription,
                RecognitionEditorLayer,
                RecognitionEditorHeight,
                RecognitionEditorNote));
        ApplyRecognitionSession(session, selectedId);
        StatusMessage =
            "Correção humana salva; nenhuma alteração foi aplicada ao DWG.";
    }

    private void OpenRecognitionRun()
    {
        if (_recognitionSession is not null)
        {
            _fileSystemLauncher.OpenFile(_recognitionSession.RunDirectory);
        }
    }

    private void ApplyRecognitionSession(
        RecognitionSession session,
        Guid? selectedId = null)
    {
        _recognitionSession = session;
        _allRecognitionCandidates.Clear();
        _allRecognitionCandidates.AddRange(
            session.Candidates.Select(RecognitionCandidateItemViewModel.From));
        ReplaceItems(
            RecognitionLegendEntries,
            (session.LegendAnalysis?.Entries
             ?? [])
            .Select(RecognitionLegendEntryItemViewModel.From));
        ReplaceItems(
            RecognitionOverlayPoints,
            session.Candidates.Select(candidate => new CadOverlayPointViewModel(
                candidate.Id,
                candidate.Handle,
                candidate.PositionX,
                candidate.PositionY,
                RecognitionCandidateItemViewModel.GetAccent(candidate),
                $"{candidate.Handle} • {candidate.ProposedDescription}")));
        ApplyRecognitionFilter();
        SelectedRecognitionCandidate = selectedId is null
            ? RecognitionCandidates.FirstOrDefault()
            : _allRecognitionCandidates.FirstOrDefault(
                candidate => candidate.Id == selectedId);
        RecognitionSummary =
            $"{session.Candidates.Count} blocos candidatos; "
            + $"{RecognitionHighConfidenceCount} reconhecidos com alta confiança; "
            + $"{RecognitionNeedsReviewCount} exigem revisão; "
            + $"{session.LegendAnalysis?.Entries.Count ?? 0} referência(s) "
            + "extraída(s) da legenda.";
        NotifyRecognitionChanged();
    }

    private void ApplyRecognitionFilter()
    {
        IEnumerable<RecognitionCandidateItemViewModel> query =
            _allRecognitionCandidates;
        query = SelectedRecognitionFilter switch
        {
            "Alta confiança" => query.Where(item =>
                item.ConfidenceScore >= 85),
            "Identificados pela legenda" => query.Where(item =>
                item.LegendMatched),
            "Revisão obrigatória" => query.Where(item =>
                item.ConfidenceScore < 85
                && item.StatusValue == RecognitionCandidateStatus.Pending),
            "Não classificados" => query.Where(item =>
                string.IsNullOrWhiteSpace(item.Code)),
            "Pendentes" => query.Where(item =>
                item.StatusValue == RecognitionCandidateStatus.Pending),
            "Aprovados" => query.Where(item =>
                item.StatusValue == RecognitionCandidateStatus.Approved),
            "Corrigidos" => query.Where(item =>
                item.StatusValue == RecognitionCandidateStatus.Corrected),
            "Rejeitados" => query.Where(item =>
                item.StatusValue == RecognitionCandidateStatus.Rejected),
            _ => query
        };
        ReplaceItems(RecognitionCandidates, query);
    }

    private void ApplyRecognitionCadLayers()
    {
        ReplaceItems(
            RecognitionCadLayers,
            RecognitionCadSnapshot?.Layers.Select(CadLayerItemViewModel.From)
            ?? []);
    }

    private void LoadRecognitionEditor(
        RecognitionCandidateItemViewModel? item)
    {
        if (item is null)
        {
            RecognitionEditorDiscipline = "Elétrico";
            RecognitionEditorCode = string.Empty;
            RecognitionEditorDescription = string.Empty;
            RecognitionEditorLayer = string.Empty;
            RecognitionEditorHeight = string.Empty;
            RecognitionEditorNote = string.Empty;
            return;
        }

        RecognitionEditorDiscipline =
            item.Discipline == "Hidráulico" ? "Hidráulico" : "Elétrico";
        RecognitionEditorCode = item.Code;
        RecognitionEditorDescription = item.Description;
        RecognitionEditorLayer = item.SemanticLayer;
        RecognitionEditorHeight = item.Height;
        RecognitionEditorNote = item.Note;
    }

    private void ClearRecognition(string message)
    {
        _recognitionSession = null;
        RecognitionCadSnapshot = null;
        _allRecognitionCandidates.Clear();
        ReplaceItems(RecognitionCandidates, []);
        ReplaceItems(RecognitionOverlayPoints, []);
        ReplaceItems(RecognitionCadLayers, []);
        ReplaceItems(RecognitionLegendEntries, []);
        SelectedRecognitionCandidate = null;
        RecognitionStage = "AGUARDANDO NOVO DWG";
        RecognitionStageAccent = "#627087";
        RecognitionSummary = message;
        NotifyRecognitionChanged();
    }

    private void NotifyRecognitionChanged()
    {
        OnPropertyChanged(nameof(RecognitionCandidateCount));
        OnPropertyChanged(nameof(RecognitionHighConfidenceCount));
        OnPropertyChanged(nameof(RecognitionNeedsReviewCount));
        OnPropertyChanged(nameof(RecognitionReviewedCount));
        OnPropertyChanged(nameof(RecognitionInventorySummary));
        OnPropertyChanged(nameof(RecognitionIntegrity));
        OnPropertyChanged(nameof(RecognitionProfileSource));
        OnPropertyChanged(nameof(RecognitionRunDirectory));
        OnPropertyChanged(nameof(RecognitionLegendStatus));
        OnPropertyChanged(nameof(RecognitionLegendAccent));
        OnPropertyChanged(nameof(RecognitionLegendSummary));
        RaiseRecognitionCommandStates();
    }

    private void RaiseRecognitionCommandStates()
    {
        (ChooseRecognitionDwgCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
        (AnalyzeRecognitionCommand as AsyncCommand)?
            .RaiseCanExecuteChanged();
        (SaveRecognitionCorrectionCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
        (OpenRecognitionRunCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
    }

    private void ReportRecognitionFailure(Exception exception)
    {
        RecognitionStage = "FALHA ISOLADA";
        RecognitionStageAccent = "#FF5D68";
        RecognitionSummary = exception.Message;
        StatusMessage =
            $"O reconhecimento não foi concluído: {exception.Message}";
        TryWriteDiagnostic(
            Application.Diagnostics.DiagnosticLevel.Error,
            "CadRecognition",
            "Falha isolada no Marco 11.6I.",
            exception);
    }
}

public sealed record RecognitionCandidateItemViewModel(
    Guid Id,
    string Handle,
    string ObjectType,
    string SourceLayer,
    string BlockName,
    string Position,
    string Rotation,
    string Discipline,
    string Code,
    string Description,
    string SemanticLayer,
    string Height,
    string Confidence,
    int ConfidenceScore,
    string Reason,
    string Status,
    string StatusAccent,
    string Note,
    RecognitionCandidateStatus StatusValue,
    bool LegendMatched = false,
    string LegendDescription = "",
    string LegendEvidence = "",
    string RecognitionOrigin = "",
    string RootHandle = "",
    string StablePath = "",
    string GeometrySignature = "",
    int ExpansionDepth = 0)
{
    public static RecognitionCandidateItemViewModel From(
        RecognitionCandidate candidate) =>
        new(
            candidate.Id,
            candidate.Handle,
            candidate.ObjectType,
            candidate.SourceLayer,
            string.IsNullOrWhiteSpace(candidate.BlockName)
                ? "Sem nome de bloco"
                : candidate.BlockName,
            $"{candidate.PositionX.ToString("0.###", CultureInfo.CurrentCulture)}; "
            + $"{candidate.PositionY.ToString("0.###", CultureInfo.CurrentCulture)}",
            $"{candidate.RotationDegrees.ToString("0.##", CultureInfo.CurrentCulture)}°",
            candidate.ProposedDiscipline == SemanticDiscipline.Hydraulic
                ? "Hidráulico"
                : candidate.ProposedDiscipline == SemanticDiscipline.Electrical
                    ? "Elétrico"
                    : "A confirmar",
            candidate.ProposedCode,
            candidate.ProposedDescription,
            candidate.ProposedLayer,
            candidate.ProposedHeight,
            candidate.Confidence switch
            {
                SemanticConfidence.High => "Alta",
                SemanticConfidence.Medium => "Média",
                SemanticConfidence.Low => "Baixa",
                _ => "Desconhecida"
            },
            candidate.ConfidenceScore,
            candidate.DetectionReason,
            candidate.Status switch
            {
                RecognitionCandidateStatus.Approved => "Aprovado",
                RecognitionCandidateStatus.Corrected => "Corrigido",
                RecognitionCandidateStatus.Rejected => "Rejeitado",
                _ => "Pendente"
            },
            GetAccent(candidate),
            candidate.ReviewNote ?? string.Empty,
            candidate.Status,
            candidate.LegendMatched,
            candidate.LegendDescription,
            candidate.LegendEvidence,
            candidate.LegendMatched
                ? candidate.MatchedByGeometryTemplate
                    ? "Legenda + geometria solta"
                    : string.IsNullOrWhiteSpace(candidate.GeometrySignature)
                    ? "Legenda + identidade do bloco"
                    : "Legenda + assinatura estrutural"
                : candidate.ExpansionDepth > 0
                    ? "Bloco aninhado"
                    : "Base histórica ou regra",
            candidate.RootHandle,
            candidate.StablePath,
            candidate.GeometrySignature,
            candidate.ExpansionDepth);

    public static string GetAccent(RecognitionCandidate candidate) =>
        candidate.Status switch
        {
            RecognitionCandidateStatus.Approved => "#36D17C",
            RecognitionCandidateStatus.Corrected => "#A78BFA",
            RecognitionCandidateStatus.Rejected => "#FF5D68",
            _ when candidate.ConfidenceScore >= 85 => "#2C9BFF",
            _ when candidate.ConfidenceScore >= 60 => "#F8C33A",
            _ => "#627087"
        };
}

public sealed record RecognitionLegendEntryItemViewModel(
    string BlockName,
    string Description,
    string SymbolHandle,
    string PairDistance,
    string Evidence,
    string Origin,
    string StablePath)
{
    public static RecognitionLegendEntryItemViewModel From(
        RecognitionLegendEntry entry) =>
        new(
            entry.BlockName,
            entry.Description,
            entry.SymbolHandle,
            entry.PairDistance.ToString(
                "0.###",
                CultureInfo.CurrentCulture),
            entry.Evidence,
            entry.IsLooseGeometry
                ? "Geometria solta + assinatura"
                : entry.ExpansionDepth > 0
                ? $"Bloco aninhado • nível {entry.ExpansionDepth}"
                : "Model Space",
            entry.StablePath);
}
