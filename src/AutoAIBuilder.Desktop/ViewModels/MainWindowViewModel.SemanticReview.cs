using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.CadVisualization;
using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Semantics;
using AutoAIBuilder.Application.Navigation;
using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private SemanticWorkspaceSnapshot _semanticSnapshot =
        SemanticWorkspaceSnapshot.Empty;
    private string _semanticSearchText = string.Empty;
    private string _selectedSemanticFilter = "Todos os pontos";
    private SemanticPointItemViewModel? _selectedSemanticPoint;
    private SemanticDirectionItemViewModel? _selectedSemanticDirection;
    private bool _isSemanticImportRunning;
    private string _semanticSummary =
        "Selecione um projeto e importe os CSVs v07/v081.";
    private string _semanticEditorDiscipline = "Elétrico";
    private string _semanticEditorCode = string.Empty;
    private string _semanticEditorDescription = string.Empty;
    private string _semanticEditorHeight = string.Empty;
    private string _semanticEditorNote = string.Empty;
    private bool _semanticAddToKnowledge = true;
    private string _semanticDirectionEditor = "DIREITA";
    private string _semanticDirectionNote = string.Empty;
    private string _semanticEditorMessage =
        "Selecione um ponto para revisar seus dados.";
    private int _selectedSemanticReviewTabIndex;
    private CadVisualizationSnapshot? _cadVisualization;
    private bool _isCadVisualizationRunning;
    private string _cadVisualizationStatus =
        "A planta arquitetônica ainda não foi gerada.";
    private string _cadLayerSearchText = string.Empty;
    private string _selectedCadLayerGroup = "Todas";

    public ObservableCollection<SemanticPointItemViewModel> SemanticPoints
    {
        get;
    }

    public ObservableCollection<SemanticMapPointItemViewModel> SemanticMapPoints
    {
        get;
    }

    public ObservableCollection<SemanticComponentItemViewModel>
        SelectedSemanticComponents { get; } = [];

    public ObservableCollection<SemanticSimilarPointItemViewModel>
        SemanticSimilarPoints { get; } = [];

    public ObservableCollection<SemanticDirectionItemViewModel>
        SemanticDirections { get; } = [];

    public ObservableCollection<SemanticReviewIssueItemViewModel>
        SemanticPendingIssues { get; } = [];

    public ObservableCollection<SemanticRevisionItemViewModel>
        SemanticRevisions { get; } = [];

    public ObservableCollection<SemanticKnowledgeItemViewModel>
        SemanticKnowledgeEntries { get; } = [];

    public ObservableCollection<CadLayerItemViewModel> CadLayers { get; } = [];

    public ObservableCollection<CadLayerItemViewModel> FilteredCadLayers
    {
        get;
    } = [];

    public ObservableCollection<CadOverlayPointViewModel>
        CadOverlayPoints { get; } = [];

    public IReadOnlyList<string> SemanticFilterOptions { get; }

    public IReadOnlyList<string> SemanticDisciplineOptions { get; } =
        ["Elétrico", "Hidráulico"];

    public IReadOnlyList<string> SemanticDirectionOptions { get; } =
        ["DIREITA", "ESQUERDA", "CIMA", "BAIXO"];

    public IReadOnlyList<string> CadLayerGroupOptions { get; } =
        ["Todas", "Arquitetura", "Elétrico", "Hidráulico", "Textos e cotas", "Outras"];

    public ICommand ImportSemanticCsvCommand { get; }

    public ICommand ApproveSemanticPointCommand { get; }

    public ICommand ReviewSemanticPointCommand { get; }

    public ICommand SaveSemanticCorrectionCommand { get; }

    public ICommand FindSimilarSemanticPointsCommand { get; }

    public ICommand ApplySemanticCorrectionToSimilarCommand { get; }

    public ICommand UndoSemanticPointRevisionCommand { get; }

    public ICommand SelectSemanticIssueCommand { get; }

    public ICommand SelectSemanticMapPointCommand { get; }

    public ICommand GenerateCadVisualizationCommand { get; }

    public ICommand SelectCadOverlayPointCommand { get; }

    public ICommand ShowAllCadLayersCommand { get; }

    public ICommand HideAllCadLayersCommand { get; }

    public ICommand ApproveSemanticDirectionCommand { get; }

    public ICommand CorrectSemanticDirectionCommand { get; }

    public ICommand UndoSemanticDirectionRevisionCommand { get; }

    public string SemanticSearchText
    {
        get => _semanticSearchText;
        set
        {
            if (SetField(ref _semanticSearchText, value))
            {
                ApplySemanticFilters();
            }
        }
    }

    public string SelectedSemanticFilter
    {
        get => _selectedSemanticFilter;
        set
        {
            if (SetField(ref _selectedSemanticFilter, value))
            {
                ApplySemanticFilters();
            }
        }
    }

    public SemanticPointItemViewModel? SelectedSemanticPoint
    {
        get => _selectedSemanticPoint;
        set
        {
            if (SetField(ref _selectedSemanticPoint, value))
            {
                OnPropertyChanged(nameof(SemanticSelectionVisibility));
                OnPropertyChanged(nameof(SemanticPointEditorVisibility));
                OnPropertyChanged(nameof(SemanticSelectedMapSummary));
                OnPropertyChanged(nameof(CadSelectedPointId));
                LoadSemanticPointEditor(value);
                RefreshSemanticMapSelection();
                (SaveSemanticCorrectionCommand as RelayCommand)?
                    .RaiseCanExecuteChanged();
                (FindSimilarSemanticPointsCommand as RelayCommand)?
                    .RaiseCanExecuteChanged();
                (ApplySemanticCorrectionToSimilarCommand as RelayCommand)?
                    .RaiseCanExecuteChanged();
                (UndoSemanticPointRevisionCommand as RelayCommand)?
                    .RaiseCanExecuteChanged();
            }
        }
    }

    public int SelectedSemanticReviewTabIndex
    {
        get => _selectedSemanticReviewTabIndex;
        set => SetField(ref _selectedSemanticReviewTabIndex, value);
    }

    public SemanticDirectionItemViewModel? SelectedSemanticDirection
    {
        get => _selectedSemanticDirection;
        set
        {
            if (SetField(ref _selectedSemanticDirection, value))
            {
                OnPropertyChanged(nameof(SemanticDirectionEditorVisibility));
                SemanticDirectionEditor =
                    value?.EffectiveDirection ?? "DIREITA";
                SemanticDirectionNote = value?.ReviewNote ?? string.Empty;
                (ApproveSemanticDirectionCommand as RelayCommand)?
                    .RaiseCanExecuteChanged();
                (CorrectSemanticDirectionCommand as RelayCommand)?
                    .RaiseCanExecuteChanged();
                (UndoSemanticDirectionRevisionCommand as RelayCommand)?
                    .RaiseCanExecuteChanged();
            }
        }
    }

    public string SemanticEditorDiscipline
    {
        get => _semanticEditorDiscipline;
        set => SetField(ref _semanticEditorDiscipline, value);
    }

    public string SemanticEditorCode
    {
        get => _semanticEditorCode;
        set => SetField(ref _semanticEditorCode, value);
    }

    public string SemanticEditorDescription
    {
        get => _semanticEditorDescription;
        set => SetField(ref _semanticEditorDescription, value);
    }

    public string SemanticEditorHeight
    {
        get => _semanticEditorHeight;
        set => SetField(ref _semanticEditorHeight, value);
    }

    public string SemanticEditorNote
    {
        get => _semanticEditorNote;
        set => SetField(ref _semanticEditorNote, value);
    }

    public bool SemanticAddToKnowledge
    {
        get => _semanticAddToKnowledge;
        set => SetField(ref _semanticAddToKnowledge, value);
    }

    public string SemanticDirectionEditor
    {
        get => _semanticDirectionEditor;
        set => SetField(ref _semanticDirectionEditor, value);
    }

    public string SemanticDirectionNote
    {
        get => _semanticDirectionNote;
        set => SetField(ref _semanticDirectionNote, value);
    }

    public string SemanticEditorMessage
    {
        get => _semanticEditorMessage;
        private set => SetField(ref _semanticEditorMessage, value);
    }

    public CadVisualizationSnapshot? CadVisualization
    {
        get => _cadVisualization;
        private set
        {
            if (SetField(ref _cadVisualization, value))
            {
                OnPropertyChanged(nameof(CadVisualizationVisibility));
                OnPropertyChanged(nameof(SemanticCoordinateMapVisibility));
                OnPropertyChanged(nameof(CadVisualizationSummary));
                OnPropertyChanged(nameof(CadFidelitySummary));
                OnPropertyChanged(nameof(CadLayerPanelVisibility));
            }
        }
    }

    public bool IsCadVisualizationRunning
    {
        get => _isCadVisualizationRunning;
        private set
        {
            if (SetField(ref _isCadVisualizationRunning, value))
            {
                OnPropertyChanged(nameof(CadVisualizationRunningVisibility));
                (GenerateCadVisualizationCommand as AsyncCommand)?
                    .RaiseCanExecuteChanged();
            }
        }
    }

    public string CadVisualizationStatus
    {
        get => _cadVisualizationStatus;
        private set => SetField(ref _cadVisualizationStatus, value);
    }

    public string CadLayerSearchText
    {
        get => _cadLayerSearchText;
        set
        {
            if (SetField(ref _cadLayerSearchText, value))
            {
                RefreshCadLayerFilter();
            }
        }
    }

    public string SelectedCadLayerGroup
    {
        get => _selectedCadLayerGroup;
        set
        {
            if (SetField(ref _selectedCadLayerGroup, value))
            {
                RefreshCadLayerFilter();
            }
        }
    }

    public bool IsSemanticImportRunning
    {
        get => _isSemanticImportRunning;
        private set
        {
            if (SetField(ref _isSemanticImportRunning, value))
            {
                OnPropertyChanged(nameof(SemanticImportRunningVisibility));
                (ImportSemanticCsvCommand as AsyncCommand)?
                    .RaiseCanExecuteChanged();
            }
        }
    }

    public string SemanticSummary
    {
        get => _semanticSummary;
        private set => SetField(ref _semanticSummary, value);
    }

    public string SemanticDatasetTitle =>
        _semanticSnapshot.Dataset is null
            ? "Nenhum conjunto importado"
            : $"{_semanticSnapshot.Dataset.DrawingFileName} • "
              + $"v{_semanticSnapshot.Dataset.SourceVersion}";

    public string SemanticDatasetPath =>
        _semanticSnapshot.Dataset?.DrawingPath
        ?? "Os arquivos originais permanecem intactos.";

    public string SemanticImportedAt =>
        _semanticSnapshot.Dataset is null
            ? "Ainda não importado"
            : $"Importado em "
              + $"{_semanticSnapshot.Dataset.UpdatedAt.ToLocalTime():dd/MM/yyyy HH:mm}";

    public string SemanticFingerprint =>
        _semanticSnapshot.Dataset is null
            ? "SHA-256 ainda não calculado"
            : $"SHA-256 combinado: "
              + $"{_semanticSnapshot.Dataset.SourceFingerprint[..12]}…";

    public string SemanticBaselineStatus =>
        _semanticSnapshot.Dataset is null
            ? "AGUARDANDO IMPORTAÇÃO"
            : _semanticSnapshot.Dataset.MatchesHistoricalBaseline
                ? "LINHA DE BASE HISTÓRICA APROVADA"
                : "DADOS IMPORTADOS • TOTAIS DIFERENTES DA BASE";

    public string SemanticBaselineAccent =>
        _semanticSnapshot.Dataset is null
            ? "#627087"
            : _semanticSnapshot.Dataset.MatchesHistoricalBaseline
                ? "#36D17C"
                : "#F8C33A";

    public string SemanticPointCount =>
        (_semanticSnapshot.Dataset?.PointCount ?? 0).ToString(
            "N0",
            CultureInfo.CurrentCulture);

    public string SemanticElectricalCount =>
        _semanticSnapshot.Points.Count(
            point => point.Discipline == SemanticDiscipline.Electrical).ToString(
            "N0",
            CultureInfo.CurrentCulture);

    public string SemanticHydraulicCount =>
        _semanticSnapshot.Points.Count(
            point => point.Discipline == SemanticDiscipline.Hydraulic).ToString(
            "N0",
            CultureInfo.CurrentCulture);

    public string SemanticComponentCount =>
        (_semanticSnapshot.Dataset?.ComponentCount ?? 0).ToString(
            "N0",
            CultureInfo.CurrentCulture);

    public string SemanticLayerCount =>
        (_semanticSnapshot.Dataset?.SemanticLayerCount ?? 0).ToString(
            "N0",
            CultureInfo.CurrentCulture);

    public string SemanticDirectionSummary =>
        _semanticSnapshot.Dataset is null
            ? "0 diagnósticos"
            : $"{_semanticSnapshot.Dataset.DirectionCount} diagnósticos • "
              + $"{_semanticSnapshot.Dataset.DirectionReviewCount} para revisão • "
              + $"{_semanticSnapshot.Dataset.BuilderValidatedDirectionCount} "
              + "validados no Builder";

    public string SemanticReviewProgress
    {
        get
        {
            var total = _semanticSnapshot.Points.Count;
            var approved = _semanticSnapshot.Points.Count(
                point => point.ReviewStatus == SemanticReviewStatus.Approved);
            var needsReview = _semanticSnapshot.Points.Count(
                point => point.ReviewStatus == SemanticReviewStatus.NeedsReview);
            return total == 0
                ? "Nenhum ponto para revisar."
                : $"{approved} aprovado(s) manualmente • "
                  + $"{needsReview} marcado(s) para revisão";
        }
    }

    public string SemanticFilteredCount =>
        $"{SemanticPoints.Count:N0} ponto(s) exibido(s)";

    public string SemanticPendingCount =>
        $"{SemanticPendingIssues.Count:N0} pendência(s)";

    public string SemanticKnowledgeSummary =>
        $"{SemanticKnowledgeEntries.Count:N0} padrão(ões) do projeto";

    public Guid? CadSelectedPointId => SelectedSemanticPoint?.Id;

    public string CadVisualizationSummary =>
        CadVisualization is null
            ? GetCadEngineSummary()
            : $"{CadVisualization.Primitives.Count:N0} elementos • "
              + $"{CadVisualization.Layers.Count:N0} layers • "
              + $"{CadVisualization.EngineName} "
              + CadVisualization.EngineVersion;

    public string CadFidelitySummary =>
        CadVisualization is null
            ? "Cobertura ainda não calculada"
            : $"Cobertura vetorial {CadVisualization.Coverage.Percentage:0.0}% • "
              + (CadVisualization.Coverage.RemainingBlockCount == 0
                  ? "todos os blocos processados"
                  : $"{CadVisualization.Coverage.RemainingBlockCount} "
                    + "bloco(s) especial(is) sinalizado(s)");

    public string SemanticSelectedMapSummary =>
        SelectedSemanticPoint is null
            ? "Selecione um ponto na tabela ou no mapa."
            : $"PONTO SELECIONADO • {SelectedSemanticPoint.ExternalId} • "
              + $"{SelectedSemanticPoint.Description} • "
              + SelectedSemanticPoint.Coordinates;

    public Visibility SemanticHasDataVisibility =>
        _semanticSnapshot.Dataset is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility SemanticEmptyVisibility =>
        _semanticSnapshot.Dataset is null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility CadVisualizationVisibility =>
        CadVisualization is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility SemanticCoordinateMapVisibility =>
        CadVisualization is null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility CadLayerPanelVisibility =>
        CadVisualization is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility CadVisualizationRunningVisibility =>
        IsCadVisualizationRunning
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SemanticSelectionVisibility =>
        SelectedSemanticPoint is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility SemanticPointEditorVisibility =>
        SelectedSemanticPoint is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility SemanticDirectionEditorVisibility =>
        SelectedSemanticDirection is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility SemanticSimilarVisibility =>
        SemanticSimilarPoints.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility SemanticPendingEmptyVisibility =>
        SemanticPendingIssues.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SemanticImportRunningVisibility =>
        IsSemanticImportRunning
            ? Visibility.Visible
            : Visibility.Collapsed;

    private async Task ImportSemanticCsvAsync(
        CancellationToken cancellationToken)
    {
        if (SelectedProject is null)
        {
            StatusMessage =
                "Selecione um projeto antes de importar dados semânticos.";
            return;
        }

        var paths = _filePicker.PickSemanticCsvFiles();
        if (paths.Count == 0)
        {
            SemanticSummary = "Seleção cancelada; nenhum arquivo foi alterado.";
            return;
        }

        IsSemanticImportRunning = true;
        SemanticSummary =
            "Validando cabeçalhos, vínculos, números e SHA-256 dos CSVs.";
        try
        {
            var result = await _semanticWorkspaceService.ImportAsync(
                SelectedProject.Id,
                paths,
                cancellationToken);
            RefreshSemanticReview();
            var baseline = result.Dataset.MatchesHistoricalBaseline
                ? " A linha de base histórica foi reproduzida."
                : " Os totais precisam ser comparados com a linha de base.";
            var warnings = result.Warnings.Count == 0
                ? string.Empty
                : $" {string.Join(" ", result.Warnings)}";
            SemanticSummary =
                $"{result.Dataset.PointCount} pontos e "
                + $"{result.Dataset.ComponentCount} componentes importados."
                + baseline
                + warnings;
            TryRecordActivity(
                "Análise semântica",
                result.ReplacedExistingDataset
                    ? "Conjunto semântico atualizado"
                    : "Conjunto semântico importado",
                SemanticSummary,
                result.Dataset.MatchesHistoricalBaseline
                    ? ActivityLevel.Success
                    : ActivityLevel.Warning,
                SelectedProject);
            StatusMessage = result.Dataset.MatchesHistoricalBaseline
                ? "Importação semântica concluída e linha de base aprovada."
                : "Importação semântica concluída com totais que exigem revisão.";
        }
        finally
        {
            IsSemanticImportRunning = false;
        }
    }

    private void ReportSemanticImportFailure(Exception exception)
    {
        var message = exception is InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException
                ? GetFriendlyMessage(exception)
                : "Falha inesperada durante a importação semântica.";
        SemanticSummary = message;
        StatusMessage = $"Não foi possível importar os CSVs: {message}";
        TryWriteDiagnostic(
            DiagnosticLevel.Error,
            "SemanticImport",
            "A importação semântica foi bloqueada.",
            exception);
    }

    private void RefreshSemanticReview()
    {
        var selectedExternalId = SelectedSemanticPoint?.ExternalId;
        var selectedDirectionHandle = SelectedSemanticDirection?.Handle;
        if (SelectedProject is null)
        {
            _semanticSnapshot = SemanticWorkspaceSnapshot.Empty;
            SemanticSummary =
                "Selecione um projeto e importe os CSVs v07/v081.";
        }
        else
        {
            try
            {
                _semanticSnapshot =
                    _semanticWorkspaceService.GetForProject(SelectedProject.Id);
                SemanticSummary = _semanticSnapshot.Dataset is null
                    ? "Nenhum dado semântico foi importado para este projeto."
                    : $"{_semanticSnapshot.Dataset.PointCount} pontos carregados "
                      + "somente para consulta e revisão.";
            }
            catch (Exception exception) when (
                exception is InvalidDataException
                    or IOException
                    or InvalidOperationException)
            {
                _semanticSnapshot = SemanticWorkspaceSnapshot.Empty;
                SemanticSummary =
                    "Os dados semânticos persistidos não puderam ser carregados.";
                TryWriteDiagnostic(
                    DiagnosticLevel.Error,
                    "SemanticReview",
                    SemanticSummary,
                    exception);
            }
        }

        NotifySemanticDatasetChanged();
        ReplaceItems(
            SemanticDirections,
            _semanticSnapshot.Directions.Select(
                SemanticDirectionItemViewModel.From));
        ReplaceItems(
            SemanticPendingIssues,
            BuildPendingIssues(_semanticSnapshot));
        ReplaceItems(
            SemanticRevisions,
            _semanticSnapshot.Revisions.Select(
                SemanticRevisionItemViewModel.From));
        ReplaceItems(
            SemanticKnowledgeEntries,
            _semanticSnapshot.KnowledgeEntries.Select(
                SemanticKnowledgeItemViewModel.From));
        OnPropertyChanged(nameof(SemanticPendingCount));
        OnPropertyChanged(nameof(SemanticKnowledgeSummary));
        OnPropertyChanged(nameof(SemanticPendingEmptyVisibility));
        ApplySemanticFilters();
        SelectedSemanticPoint = SemanticPoints.FirstOrDefault(
            point => point.ExternalId.Equals(
                selectedExternalId,
                StringComparison.OrdinalIgnoreCase));
        SelectedSemanticDirection = SemanticDirections.FirstOrDefault(
            direction => direction.Handle.Equals(
                selectedDirectionHandle,
                StringComparison.OrdinalIgnoreCase));
        TryLoadCachedCadVisualization();
    }

    private void ApplySemanticFilters()
    {
        IEnumerable<SemanticPoint> query = _semanticSnapshot.Points;
        query = SelectedSemanticFilter switch
        {
            "Elétricos" => query.Where(
                point => point.Discipline == SemanticDiscipline.Electrical),
            "Hidráulicos" => query.Where(
                point => point.Discipline == SemanticDiscipline.Hydraulic),
            "Precisam de revisão" => query.Where(
                point => point.ReviewStatus == SemanticReviewStatus.NeedsReview),
            "Aprovados" => query.Where(
                point => point.ReviewStatus == SemanticReviewStatus.Approved),
            _ => query
        };

        var search = SemanticSearchText.Trim();
        if (search.Length > 0)
        {
            query = query.Where(point =>
                point.SemanticCode.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || point.Description.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || point.Handle.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || point.SemanticLayer.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || point.AssociatedTexts.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase));
        }

        var items = query
            .OrderBy(point => point.SemanticCode)
            .ThenBy(point => point.ExternalId)
            .Select(SemanticPointItemViewModel.From)
            .ToArray();
        ReplaceItems(SemanticPoints, items);
        ReplaceItems(
            SemanticMapPoints,
            BuildMapPoints(items, SelectedSemanticPoint?.Id));
        OnPropertyChanged(nameof(SemanticFilteredCount));
        SelectedSemanticPoint =
            SemanticPoints.FirstOrDefault(
                point => point.Id == SelectedSemanticPoint?.Id);
    }

    private void UpdateSemanticPointReview(
        SemanticPointItemViewModel? point,
        SemanticReviewStatus status)
    {
        if (SelectedProject is null || point is null)
        {
            return;
        }

        try
        {
            _semanticWorkspaceService.UpdatePointReview(
                SelectedProject.Id,
                point.Id,
                status);
            var externalId = point.ExternalId;
            RefreshSemanticReview();
            SelectedSemanticPoint = SemanticPoints.FirstOrDefault(
                item => item.ExternalId.Equals(
                    externalId,
                    StringComparison.OrdinalIgnoreCase));
            StatusMessage = status == SemanticReviewStatus.Approved
                ? $"Ponto {externalId} aprovado para este projeto."
                : $"Ponto {externalId} marcado para revisão.";
            TryRecordActivity(
                "Análise semântica",
                status == SemanticReviewStatus.Approved
                    ? "Ponto aprovado"
                    : "Ponto marcado para revisão",
                $"Estado de {externalId} alterado sem modificar o DWG.",
                ActivityLevel.Information,
                SelectedProject);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or KeyNotFoundException)
        {
            StatusMessage =
                $"Não foi possível atualizar a revisão: {exception.Message}";
        }
    }

    private void LoadSemanticPointEditor(SemanticPointItemViewModel? item)
    {
        SelectedSemanticComponents.Clear();
        SemanticSimilarPoints.Clear();
        OnPropertyChanged(nameof(SemanticSimilarVisibility));
        (ApplySemanticCorrectionToSimilarCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
        if (item is null)
        {
            SemanticEditorMessage =
                "Selecione um ponto para revisar seus dados.";
            return;
        }

        var point = _semanticSnapshot.Points.SingleOrDefault(
            candidate => candidate.Id == item.Id);
        if (point is null)
        {
            SemanticEditorMessage =
                "O ponto selecionado não está mais disponível.";
            return;
        }

        SemanticEditorDiscipline =
            point.Discipline == SemanticDiscipline.Hydraulic
                ? "Hidráulico"
                : "Elétrico";
        SemanticEditorCode = point.SemanticCode;
        SemanticEditorDescription = point.Description;
        SemanticEditorHeight = point.HeightSourceValue;
        SemanticEditorNote = point.ReviewNote ?? string.Empty;
        ReplaceItems(
            SelectedSemanticComponents,
            _semanticSnapshot.Components
                .Where(component => component.PointExternalId.Equals(
                    point.ExternalId,
                    StringComparison.OrdinalIgnoreCase))
                .Select(SemanticComponentItemViewModel.From));
        SemanticEditorMessage =
            $"Classificação baseada na fonte {point.Source}, confiança "
            + $"{GetConfidenceLabel(point.Confidence)}, camada "
            + $"{point.SemanticLayer} e bloco "
            + $"{(string.IsNullOrWhiteSpace(point.BlockName)
                ? "não nomeado"
                : point.BlockName)}. Foram associados "
            + $"{point.GraphicComponentCount} componente(s) gráfico(s) e "
            + $"{point.AssociatedTextCount} texto(s).";
    }

    private void SaveSemanticCorrection()
    {
        if (SelectedProject is null || SelectedSemanticPoint is null)
        {
            return;
        }

        var externalId = SelectedSemanticPoint.ExternalId;
        var similarIds = SemanticSimilarPoints
            .Select(point => point.Id)
            .ToArray();
        try
        {
            _semanticWorkspaceService.CorrectPoint(
                SelectedProject.Id,
                SelectedSemanticPoint.Id,
                new SemanticPointCorrectionRequest(
                    SemanticEditorDiscipline == "Hidráulico"
                        ? SemanticDiscipline.Hydraulic
                        : SemanticDiscipline.Electrical,
                    SemanticEditorCode,
                    SemanticEditorDescription,
                    SemanticEditorHeight,
                    SemanticEditorNote,
                    SemanticAddToKnowledge));
            RefreshSemanticReview();
            SelectedSemanticPoint = SemanticPoints.FirstOrDefault(
                point => point.ExternalId.Equals(
                    externalId,
                    StringComparison.OrdinalIgnoreCase));
            RestoreSimilarCandidates(similarIds);
            StatusMessage =
                $"Correção de {externalId} salva com histórico reversível.";
            TryRecordActivity(
                "Análise semântica",
                "Ponto corrigido",
                $"Classificação de {externalId} revisada sem alterar o DWG.",
                ActivityLevel.Success,
                SelectedProject);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or KeyNotFoundException)
        {
            StatusMessage =
                $"Não foi possível salvar a correção: {exception.Message}";
        }
    }

    private void FindSimilarSemanticPoints()
    {
        if (SelectedProject is null || SelectedSemanticPoint is null)
        {
            return;
        }

        try
        {
            var candidates = _semanticWorkspaceService.FindSimilarPoints(
                SelectedProject.Id,
                SelectedSemanticPoint.Id);
            ReplaceItems(
                SemanticSimilarPoints,
                candidates.Select(SemanticSimilarPointItemViewModel.From));
            OnPropertyChanged(nameof(SemanticSimilarVisibility));
            (ApplySemanticCorrectionToSimilarCommand as RelayCommand)?
                .RaiseCanExecuteChanged();
            SemanticEditorMessage = candidates.Count == 0
                ? "Nenhum ponto com o mesmo bloco e camada foi encontrado."
                : $"{candidates.Count} candidato(s) encontrado(s). Revise a "
                  + "lista e salve a correção do ponto de referência antes "
                  + "de aplicar o padrão.";
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or KeyNotFoundException)
        {
            StatusMessage =
                $"Não foi possível localizar semelhantes: {exception.Message}";
        }
    }

    private void ApplySemanticCorrectionToSimilar()
    {
        if (SelectedProject is null
            || SelectedSemanticPoint is null
            || SemanticSimilarPoints.Count == 0)
        {
            return;
        }

        if (SelectedSemanticPoint.ReviewStatusKey
            is not (SemanticReviewStatus.Corrected
                or SemanticReviewStatus.Approved))
        {
            StatusMessage =
                "Salve ou aprove o ponto de referência antes de aplicar aos semelhantes.";
            return;
        }

        if (!_dialogService.ConfirmApplySemanticCorrection(
                SelectedSemanticPoint.ExternalId,
                SemanticSimilarPoints.Count))
        {
            StatusMessage =
                "Aplicação em semelhantes cancelada; nada foi alterado.";
            return;
        }

        var externalId = SelectedSemanticPoint.ExternalId;
        try
        {
            var updated = _semanticWorkspaceService.ApplyPointCorrection(
                SelectedProject.Id,
                SelectedSemanticPoint.Id,
                SemanticSimilarPoints.Select(point => point.Id).ToArray(),
                $"Padrão confirmado a partir de {externalId}.");
            RefreshSemanticReview();
            SelectedSemanticPoint = SemanticPoints.FirstOrDefault(
                point => point.ExternalId.Equals(
                    externalId,
                    StringComparison.OrdinalIgnoreCase));
            StatusMessage =
                $"Correção aplicada a {updated} ponto(s) semelhante(s), "
                + "com histórico individual.";
            TryRecordActivity(
                "Análise semântica",
                "Correção aplicada por semelhança",
                $"{updated} ponto(s) receberam o padrão de {externalId}.",
                ActivityLevel.Success,
                SelectedProject);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or KeyNotFoundException)
        {
            StatusMessage =
                $"Não foi possível aplicar a correção: {exception.Message}";
        }
    }

    private void UndoSelectedSemanticPointRevision()
    {
        if (SelectedProject is null || SelectedSemanticPoint is null)
        {
            return;
        }

        var externalId = SelectedSemanticPoint.ExternalId;
        try
        {
            var undone = _semanticWorkspaceService.UndoLatestRevision(
                SelectedProject.Id,
                SemanticReviewEntityKind.Point,
                SelectedSemanticPoint.Id);
            RefreshSemanticReview();
            SelectedSemanticPoint = SemanticPoints.FirstOrDefault(
                point => point.ExternalId.Equals(
                    externalId,
                    StringComparison.OrdinalIgnoreCase));
            StatusMessage = undone
                ? $"Última alteração de {externalId} desfeita."
                : $"Não há alteração ativa para desfazer em {externalId}.";
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or KeyNotFoundException)
        {
            StatusMessage =
                $"Não foi possível desfazer: {exception.Message}";
        }
    }

    private void SelectSemanticIssue(SemanticReviewIssueItemViewModel? issue)
    {
        if (issue is null)
        {
            return;
        }

        if (issue.EntityKind == SemanticReviewEntityKind.Point)
        {
            SelectedSemanticReviewTabIndex = 0;
            SemanticSearchText = string.Empty;
            SelectedSemanticFilter = "Todos os pontos";
            SelectedSemanticPoint = SemanticPoints.FirstOrDefault(
                point => point.Id == issue.EntityId)
                ?? _semanticSnapshot.Points
                    .Where(point => point.Id == issue.EntityId)
                    .Select(SemanticPointItemViewModel.From)
                    .FirstOrDefault();
            StatusMessage =
                $"Ponto {issue.ExternalId} carregado no editor detalhado.";
        }
        else
        {
            SelectedSemanticReviewTabIndex = 2;
            SelectedSemanticDirection = SemanticDirections.FirstOrDefault(
                direction => direction.Id == issue.EntityId);
            StatusMessage =
                $"Direção {issue.ExternalId} selecionada para revisão.";
        }
    }

    private void SelectSemanticMapPoint(SemanticMapPointItemViewModel? mapPoint)
    {
        if (mapPoint is null)
        {
            return;
        }

        SelectedSemanticPoint = SemanticPoints.FirstOrDefault(
            point => point.Id == mapPoint.PointId);
        if (SelectedSemanticPoint is not null)
        {
            SemanticEditorMessage =
                $"Ponto {SelectedSemanticPoint.ExternalId} selecionado no mapa.";
            StatusMessage = SemanticEditorMessage;
        }
    }

    private void SelectCadOverlayPoint(CadOverlayPointViewModel? overlayPoint)
    {
        if (overlayPoint is null)
        {
            return;
        }

        SelectedSemanticReviewTabIndex = 0;
        SemanticSearchText = string.Empty;
        SelectedSemanticFilter = "Todos os pontos";
        SelectedSemanticPoint = SemanticPoints.FirstOrDefault(
            point => point.Id == overlayPoint.PointId);
        if (SelectedSemanticPoint is not null)
        {
            SemanticEditorMessage =
                $"Ponto {SelectedSemanticPoint.ExternalId} selecionado na planta.";
            StatusMessage = SemanticEditorMessage;
        }
    }

    private async Task GenerateCadVisualizationAsync(
        CancellationToken cancellationToken)
    {
        if (SelectedProject is null || _semanticSnapshot.Dataset is null)
        {
            StatusMessage =
                "Importe os dados semânticos antes de gerar a planta.";
            return;
        }

        IsCadVisualizationRunning = true;
        CadVisualizationStatus =
            "Criando cópia técnica e solicitando a leitura ao AutoCAD.";
        try
        {
            var snapshot = await _cadVisualizationService.GenerateAsync(
                SelectedProject.Id,
                _semanticSnapshot.Dataset.DrawingPath,
                cancellationToken);
            ApplyCadVisualization(snapshot);
            if (CurrentSection == WorkspaceSection.AutomationPreview)
            {
                RefreshAutomationPreview();
            }
            CadVisualizationStatus = snapshot.LoadedFromCache
                ? "Visualização verificada carregada do cache local."
                : "Planta gerada pelo AutoCAD sobre uma cópia técnica.";
            StatusMessage = CadVisualizationStatus;
            TryRecordActivity(
                "Análise semântica",
                "Visualização CAD gerada",
                $"{snapshot.Primitives.Count} elementos e "
                + $"{snapshot.Layers.Count} layers carregados sem alterar o DWG.",
                ActivityLevel.Success,
                SelectedProject);
        }
        finally
        {
            IsCadVisualizationRunning = false;
        }
    }

    private void ReportCadVisualizationFailure(Exception exception)
    {
        CadVisualizationStatus =
            $"Não foi possível gerar a planta: {GetFriendlyMessage(exception)}";
        StatusMessage = CadVisualizationStatus;
        TryWriteDiagnostic(
            DiagnosticLevel.Error,
            "CadVisualization",
            "A ponte gráfica com o AutoCAD foi interrompida.",
            exception);
    }

    private void TryLoadCachedCadVisualization()
    {
        if (SelectedProject is null || _semanticSnapshot.Dataset is null)
        {
            CadVisualization = null;
            ReplaceItems(CadLayers, []);
            ReplaceItems(FilteredCadLayers, []);
            ReplaceItems(CadOverlayPoints, []);
            return;
        }

        try
        {
            var cached = _cadVisualizationService.TryLoadCached(
                SelectedProject.Id,
                _semanticSnapshot.Dataset.DrawingPath);
            if (cached is null)
            {
                CadVisualization = null;
                ReplaceItems(CadLayers, []);
                ReplaceItems(FilteredCadLayers, []);
                RefreshCadOverlayPoints();
                CadVisualizationStatus =
                    "Gere a planta usando a cópia técnica do DWG.";
            }
            else
            {
                ApplyCadVisualization(cached);
                CadVisualizationStatus =
                    "Visualização verificada carregada do cache local.";
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or UnauthorizedAccessException)
        {
            CadVisualization = null;
            CadVisualizationStatus =
                $"Cache gráfico indisponível: {exception.Message}";
        }
    }

    private void ApplyCadVisualization(CadVisualizationSnapshot snapshot)
    {
        CadVisualization = snapshot;
        ReplaceItems(
            CadLayers,
            snapshot.Layers.Select(CadLayerItemViewModel.From));
        RefreshCadLayerFilter();
        RefreshCadOverlayPoints();
        OnPropertyChanged(nameof(CadVisualizationSummary));
        OnPropertyChanged(nameof(CadFidelitySummary));
        OnPropertyChanged(nameof(CadLayerPanelVisibility));
    }

    private void RefreshCadLayerFilter()
    {
        var query = CadLayerSearchText.Trim();
        var layers = CadLayers.Where(layer =>
            (SelectedCadLayerGroup == "Todas"
             || layer.Group == SelectedCadLayerGroup)
            && (query.Length == 0
                || layer.Name.Contains(
                    query,
                    StringComparison.CurrentCultureIgnoreCase)));
        ReplaceItems(FilteredCadLayers, layers);
    }

    private void SetCadLayerVisibility(bool isVisible)
    {
        foreach (var layer in FilteredCadLayers)
        {
            layer.IsVisible = isVisible;
        }
    }

    private void RefreshCadOverlayPoints()
    {
        ReplaceItems(
            CadOverlayPoints,
            _semanticSnapshot.Points.Select(CadOverlayPointViewModel.From));
    }

    private string GetCadEngineSummary()
    {
        var engine = _cadVisualizationService.GetEngineStatus();
        return engine.IsAvailable
            ? $"{engine.EngineName} {engine.Version} disponível"
            : engine.Message;
    }

    private void SaveSemanticDirection(SemanticReviewStatus status)
    {
        if (SelectedProject is null || SelectedSemanticDirection is null)
        {
            return;
        }

        var handle = SelectedSemanticDirection.Handle;
        try
        {
            _semanticWorkspaceService.ReviewDirection(
                SelectedProject.Id,
                SelectedSemanticDirection.Id,
                new SemanticDirectionReviewRequest(
                    SemanticDirectionEditor,
                    status,
                    SemanticDirectionNote));
            RefreshSemanticReview();
            SelectedSemanticDirection = SemanticDirections.FirstOrDefault(
                direction => direction.Handle.Equals(
                    handle,
                    StringComparison.OrdinalIgnoreCase));
            StatusMessage = status == SemanticReviewStatus.Approved
                ? $"Direção {handle} aprovada."
                : $"Direção {handle} corrigida.";
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or KeyNotFoundException)
        {
            StatusMessage =
                $"Não foi possível revisar a direção: {exception.Message}";
        }
    }

    private void UndoSelectedSemanticDirectionRevision()
    {
        if (SelectedProject is null || SelectedSemanticDirection is null)
        {
            return;
        }

        var handle = SelectedSemanticDirection.Handle;
        try
        {
            var undone = _semanticWorkspaceService.UndoLatestRevision(
                SelectedProject.Id,
                SemanticReviewEntityKind.Direction,
                SelectedSemanticDirection.Id);
            RefreshSemanticReview();
            SelectedSemanticDirection = SemanticDirections.FirstOrDefault(
                direction => direction.Handle.Equals(
                    handle,
                    StringComparison.OrdinalIgnoreCase));
            StatusMessage = undone
                ? $"Última revisão da direção {handle} desfeita."
                : $"Não há revisão ativa para desfazer em {handle}.";
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or KeyNotFoundException)
        {
            StatusMessage =
                $"Não foi possível desfazer a direção: {exception.Message}";
        }
    }

    private void RestoreSimilarCandidates(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        ReplaceItems(
            SemanticSimilarPoints,
            _semanticSnapshot.Points
                .Where(point => ids.Contains(point.Id))
                .Select(SemanticSimilarPointItemViewModel.From));
        OnPropertyChanged(nameof(SemanticSimilarVisibility));
        (ApplySemanticCorrectionToSimilarCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
    }

    private static IReadOnlyList<SemanticReviewIssueItemViewModel>
        BuildPendingIssues(SemanticWorkspaceSnapshot snapshot)
    {
        var issues = new List<SemanticReviewIssueItemViewModel>();
        foreach (var point in snapshot.Points)
        {
            if (point.ReviewStatus == SemanticReviewStatus.NeedsReview)
            {
                issues.Add(SemanticReviewIssueItemViewModel.ForPoint(
                    point,
                    "Ponto marcado para revisão",
                    point.ReviewNote
                    ?? "A classificação precisa de confirmação humana.",
                    "#F8C33A"));
            }

            if (point.HeightSourceValue.Contains(
                    "A_CONFIRMAR",
                    StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(SemanticReviewIssueItemViewModel.ForPoint(
                    point,
                    "Altura a confirmar",
                    point.HeightSourceValue.Replace('_', ' '),
                    "#F8C33A"));
            }

            if (point.Confidence is SemanticConfidence.Low
                or SemanticConfidence.Medium)
            {
                issues.Add(SemanticReviewIssueItemViewModel.ForPoint(
                    point,
                    "Confiança abaixo de alta",
                    $"Confiança informada: {GetConfidenceLabel(point.Confidence)}.",
                    "#FF8A65"));
            }
        }

        foreach (var direction in snapshot.Directions.Where(direction =>
                     direction.NeedsReview
                     && direction.ReviewStatus
                         is not (SemanticReviewStatus.Approved
                             or SemanticReviewStatus.Corrected)))
        {
            issues.Add(SemanticReviewIssueItemViewModel.ForDirection(
                direction,
                "Direção Builder duvidosa",
                $"Rotação {direction.RotationDegrees:0.##}° • sugestão "
                + direction.SuggestedBuilderDirection,
                "#F8C33A"));
        }

        return issues
            .OrderBy(issue => issue.EntityKind)
            .ThenBy(issue => issue.ExternalId)
            .ToArray();
    }

    private static string GetConfidenceLabel(
        SemanticConfidence confidence) => confidence switch
    {
        SemanticConfidence.High => "alta",
        SemanticConfidence.Medium => "média",
        SemanticConfidence.Low => "baixa",
        _ => "não informada"
    };

    private void NotifySemanticDatasetChanged()
    {
        OnPropertyChanged(nameof(SemanticDatasetTitle));
        OnPropertyChanged(nameof(SemanticDatasetPath));
        OnPropertyChanged(nameof(SemanticImportedAt));
        OnPropertyChanged(nameof(SemanticFingerprint));
        OnPropertyChanged(nameof(SemanticBaselineStatus));
        OnPropertyChanged(nameof(SemanticBaselineAccent));
        OnPropertyChanged(nameof(SemanticPointCount));
        OnPropertyChanged(nameof(SemanticElectricalCount));
        OnPropertyChanged(nameof(SemanticHydraulicCount));
        OnPropertyChanged(nameof(SemanticComponentCount));
        OnPropertyChanged(nameof(SemanticLayerCount));
        OnPropertyChanged(nameof(SemanticDirectionSummary));
        OnPropertyChanged(nameof(SemanticReviewProgress));
        OnPropertyChanged(nameof(SemanticHasDataVisibility));
        OnPropertyChanged(nameof(SemanticEmptyVisibility));
    }

    private void RefreshSemanticMapSelection()
    {
        ReplaceItems(
            SemanticMapPoints,
            BuildMapPoints(SemanticPoints.ToArray(), SelectedSemanticPoint?.Id));
    }

    private static IReadOnlyList<SemanticMapPointItemViewModel> BuildMapPoints(
        IReadOnlyList<SemanticPointItemViewModel> points,
        Guid? selectedPointId)
    {
        if (points.Count == 0)
        {
            return [];
        }

        const double width = 720;
        const double height = 175;
        const double padding = 12;
        var minimumX = points.Min(point => point.CenterX);
        var maximumX = points.Max(point => point.CenterX);
        var minimumY = points.Min(point => point.CenterY);
        var maximumY = points.Max(point => point.CenterY);
        var rangeX = Math.Max(maximumX - minimumX, 1);
        var rangeY = Math.Max(maximumY - minimumY, 1);

        return points.Select(point =>
        {
            var centerLeft = padding
                + ((point.CenterX - minimumX) / rangeX)
                * (width - (2 * padding));
            var centerTop = padding
                + ((maximumY - point.CenterY) / rangeY)
                * (height - (2 * padding));
            var isSelected = point.Id == selectedPointId;
            var containerSize = isSelected ? 24d : Math.Max(point.MapSize, 8d);
            return new SemanticMapPointItemViewModel(
                point.Id,
                centerLeft - (containerSize / 2),
                centerTop - (containerSize / 2),
                isSelected ? "#FFF4C2" : point.MapAccent,
                isSelected ? 10d : point.MapSize,
                containerSize,
                isSelected ? Visibility.Visible : Visibility.Collapsed,
                isSelected ? 100 : 0,
                $"{point.Code}\n{point.Description}\n"
                + $"X {point.CenterX:0.###} • Y {point.CenterY:0.###}");
        }).ToArray();
    }
}

public sealed record SemanticPointItemViewModel(
    Guid Id,
    string ExternalId,
    string Handle,
    string Discipline,
    SemanticDiscipline DisciplineKey,
    string Code,
    string Description,
    string Height,
    string Confidence,
    string Layer,
    string BlockName,
    string AssociatedTexts,
    string Coordinates,
    double CenterX,
    double CenterY,
    string Rotation,
    string Components,
    string ReviewStatus,
    SemanticReviewStatus ReviewStatusKey,
    string StatusAccent,
    string MapAccent,
    double MapSize)
{
    public static SemanticPointItemViewModel From(SemanticPoint point)
    {
        var discipline = point.Discipline switch
        {
            SemanticDiscipline.Electrical => "Elétrico",
            SemanticDiscipline.Hydraulic => "Hidráulico",
            _ => "Não identificado"
        };
        var reviewStatus = point.ReviewStatus switch
        {
            SemanticReviewStatus.NeedsReview => "Revisar",
            SemanticReviewStatus.Corrected => "Corrigido",
            SemanticReviewStatus.Approved => "Aprovado",
            _ => "Identificado"
        };
        var statusAccent = point.ReviewStatus switch
        {
            SemanticReviewStatus.NeedsReview => "#F8C33A",
            SemanticReviewStatus.Corrected => "#A78BFA",
            SemanticReviewStatus.Approved => "#36D17C",
            _ => "#2C9BFF"
        };
        var mapAccent = point.ReviewStatus == SemanticReviewStatus.NeedsReview
            ? "#F8C33A"
            : point.ReviewStatus == SemanticReviewStatus.Approved
                ? "#36D17C"
                : point.Discipline == SemanticDiscipline.Hydraulic
                    ? "#28D7C0"
                    : "#2C9BFF";

        return new SemanticPointItemViewModel(
            point.Id,
            point.ExternalId,
            point.Handle,
            discipline,
            point.Discipline,
            point.SemanticCode,
            point.Description,
            point.HeightCm is not null
                ? $"{point.HeightCm:0.##} cm"
                : string.IsNullOrWhiteSpace(point.HeightSourceValue)
                    ? "Não informada"
                    : point.HeightSourceValue
                        .Replace('_', ' ')
                        .ToLowerInvariant(),
            point.Confidence switch
            {
                SemanticConfidence.High => "Alta",
                SemanticConfidence.Medium => "Média",
                SemanticConfidence.Low => "Baixa",
                _ => "Não informada"
            },
            point.SemanticLayer,
            string.IsNullOrWhiteSpace(point.BlockName)
                ? "Sem nome"
                : point.BlockName,
            string.IsNullOrWhiteSpace(point.AssociatedTexts)
                ? "Nenhum texto associado"
                : point.AssociatedTexts,
            $"X {point.CenterX:0.###} • Y {point.CenterY:0.###} • "
            + $"Z {point.CenterZ:0.###}",
            point.CenterX,
            point.CenterY,
            $"{point.RotationDegrees:0.##}°",
            $"{point.GraphicComponentCount} gráfico(s) • "
            + $"{point.AssociatedTextCount} texto(s)",
            reviewStatus,
            point.ReviewStatus,
            statusAccent,
            mapAccent,
            point.ReviewStatus == SemanticReviewStatus.NeedsReview ? 8 : 5);
    }
}

public sealed record SemanticMapPointItemViewModel(
    Guid PointId,
    double Left,
    double Top,
    string Accent,
    double DotSize,
    double ContainerSize,
    Visibility SelectionVisibility,
    int ZIndex,
    string ToolTip);

public sealed record CadOverlayPointViewModel(
    Guid PointId,
    string ExternalId,
    double X,
    double Y,
    string Accent,
    string ToolTip)
{
    public static CadOverlayPointViewModel From(SemanticPoint point)
    {
        var accent = point.ReviewStatus == SemanticReviewStatus.NeedsReview
            ? "#F8C33A"
            : point.Discipline == SemanticDiscipline.Hydraulic
                ? "#28D7C0"
                : "#2C9BFF";
        return new CadOverlayPointViewModel(
            point.Id,
            point.ExternalId,
            point.CenterX,
            point.CenterY,
            accent,
            $"{point.ExternalId} • {point.Description}");
    }
}

public sealed class CadLayerItemViewModel : INotifyPropertyChanged
{
    private bool _isVisible;

    private CadLayerItemViewModel(
        string name,
        int colorIndex,
        bool isVisible)
    {
        Name = name;
        ColorIndex = colorIndex;
        _isVisible = isVisible;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public int ColorIndex { get; }

    public string Group => GetGroup(Name);

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(IsVisible)));
        }
    }

    public static CadLayerItemViewModel From(CadLayerInfo layer) =>
        new(layer.Name, layer.ColorIndex, layer.IsVisible);

    private static string GetGroup(string name)
    {
        if (ContainsAny(name, "HID", "HIDR", "PONTOSHID"))
        {
            return "Hidráulico";
        }

        if (ContainsAny(name, "ELE", "PONTOSELE", "PONTOS_ELE"))
        {
            return "Elétrico";
        }

        if (ContainsAny(name, "TEXT", "TEXTO", "COTA", "CHAMADA", "TABELA"))
        {
            return "Textos e cotas";
        }

        if (ContainsAny(
                name,
                "ARQ",
                "PAREDE",
                "PORTA",
                "JANELA",
                "MOBILI",
                "PROJE",
                "BASE",
                "CINZA"))
        {
            return "Arquitetura";
        }

        return "Outras";
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(
            term,
            StringComparison.OrdinalIgnoreCase));
}

public sealed record SemanticComponentItemViewModel(
    string ExternalId,
    string ComponentClass,
    string ObjectType,
    string Content,
    string Distance,
    string Confidence)
{
    public static SemanticComponentItemViewModel From(
        SemanticComponent component) => new(
        component.ExternalId,
        component.ComponentClass,
        component.ObjectType,
        string.IsNullOrWhiteSpace(component.TextContent)
            ? string.IsNullOrWhiteSpace(component.BlockName)
                ? "Sem conteúdo textual"
                : component.BlockName
            : component.TextContent,
        $"{component.AssociationDistance:0.###}",
        component.AssociationConfidence switch
        {
            SemanticConfidence.High => "Alta",
            SemanticConfidence.Medium => "Média",
            SemanticConfidence.Low => "Baixa",
            _ => "Não informada"
        });
}

public sealed record SemanticSimilarPointItemViewModel(
    Guid Id,
    string ExternalId,
    string Code,
    string Description,
    string Coordinates,
    string Status)
{
    public static SemanticSimilarPointItemViewModel From(
        SemanticPoint point) => new(
        point.Id,
        point.ExternalId,
        point.SemanticCode,
        point.Description,
        $"X {point.CenterX:0.##} • Y {point.CenterY:0.##}",
        point.ReviewStatus switch
        {
            SemanticReviewStatus.NeedsReview => "Revisar",
            SemanticReviewStatus.Corrected => "Corrigido",
            SemanticReviewStatus.Approved => "Aprovado",
            _ => "Identificado"
        });
}

public sealed record SemanticDirectionItemViewModel(
    Guid Id,
    string Handle,
    string SuggestedDirection,
    string EffectiveDirection,
    string Rotation,
    string AngleType,
    string OffsetOrigin,
    bool NeedsReview,
    string ReviewStatus,
    SemanticReviewStatus ReviewStatusKey,
    string ReviewNote,
    string Accent)
{
    public static SemanticDirectionItemViewModel From(
        SemanticDirectionDiagnostic direction)
    {
        var status = direction.ReviewStatus switch
        {
            SemanticReviewStatus.NeedsReview => "Revisar",
            SemanticReviewStatus.Corrected => "Corrigida",
            SemanticReviewStatus.Approved => "Aprovada",
            _ => direction.NeedsReview ? "Revisar" : "Identificada"
        };
        var accent = direction.ReviewStatus switch
        {
            SemanticReviewStatus.Corrected => "#A78BFA",
            SemanticReviewStatus.Approved => "#36D17C",
            _ => direction.NeedsReview ? "#F8C33A" : "#2C9BFF"
        };
        return new SemanticDirectionItemViewModel(
            direction.Id,
            direction.Handle,
            direction.SuggestedBuilderDirection,
            direction.EffectiveBuilderDirection,
            $"{direction.RotationDegrees:0.##}°",
            direction.GraphicAngleType.Replace('_', ' '),
            direction.OffsetOrigin.Replace('_', ' '),
            direction.NeedsReview,
            status,
            direction.ReviewStatus,
            direction.ReviewNote ?? string.Empty,
            accent);
    }
}

public sealed record SemanticReviewIssueItemViewModel(
    SemanticReviewEntityKind EntityKind,
    Guid EntityId,
    string ExternalId,
    string Kind,
    string Title,
    string Detail,
    string Accent)
{
    public static SemanticReviewIssueItemViewModel ForPoint(
        SemanticPoint point,
        string title,
        string detail,
        string accent) => new(
        SemanticReviewEntityKind.Point,
        point.Id,
        point.ExternalId,
        "Ponto",
        title,
        detail,
        accent);

    public static SemanticReviewIssueItemViewModel ForDirection(
        SemanticDirectionDiagnostic direction,
        string title,
        string detail,
        string accent) => new(
        SemanticReviewEntityKind.Direction,
        direction.Id,
        direction.Handle,
        "Direção",
        title,
        detail,
        accent);
}

public sealed record SemanticRevisionItemViewModel(
    Guid Id,
    string Date,
    string Time,
    string Entity,
    string ExternalId,
    string Action,
    string Summary,
    string Note,
    string Status,
    string Accent)
{
    public static SemanticRevisionItemViewModel From(
        SemanticReviewRevision revision) => new(
        revision.Id,
        revision.OccurredAt.ToLocalTime().ToString("dd/MM/yyyy"),
        revision.OccurredAt.ToLocalTime().ToString("HH:mm:ss"),
        revision.EntityKind == SemanticReviewEntityKind.Point
            ? "Ponto"
            : "Direção",
        revision.EntityExternalId,
        revision.Action,
        revision.Summary,
        revision.Note ?? string.Empty,
        revision.RevertedAt is null ? "Ativa" : "Desfeita",
        revision.RevertedAt is null ? "#36D17C" : "#627087");
}

public sealed record SemanticKnowledgeItemViewModel(
    Guid Id,
    string Signature,
    string Source,
    string LearnedCode,
    string LearnedDescription,
    string LearnedHeight,
    string Evidence,
    string UpdatedAt)
{
    public static SemanticKnowledgeItemViewModel From(
        SemanticKnowledgeEntry entry) => new(
        entry.Id,
        entry.Signature,
        string.IsNullOrWhiteSpace(entry.BlockName)
            ? entry.SemanticLayer
            : $"{entry.BlockName} • {entry.SemanticLayer}",
        entry.LearnedCode,
        entry.LearnedDescription,
        entry.LearnedHeight,
        $"{entry.EvidenceCount} evidência(s)",
        entry.UpdatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
}
