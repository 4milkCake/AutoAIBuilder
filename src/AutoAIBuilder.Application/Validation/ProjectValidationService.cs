using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Application.Validation;

public sealed class ProjectValidationService
{
    public ProjectValidationReport Validate(
        ProjectWorkspace project,
        IReadOnlyList<ProjectFileInspection> fileInspections,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(fileInspections);

        var results = new List<ProjectValidationResult>
        {
            ValidateProjectData(project),
            ValidateRules(project),
            ValidateFiles(project, fileInspections),
            ValidateLayers(project)
        };

        return new ProjectValidationReport(
            project.Id,
            now ?? DateTimeOffset.Now,
            results,
            project.Rules?.BlockAutomationOnValidationErrors ?? true);
    }

    private static ProjectValidationResult ValidateProjectData(ProjectWorkspace project)
    {
        var problems = new List<string>();

        if (project.IsArchived)
        {
            problems.Add("o projeto está arquivado");
        }

        if (string.IsNullOrWhiteSpace(project.Name) || string.IsNullOrWhiteSpace(project.Type))
        {
            problems.Add("nome ou tipo não informado");
        }

        if (project.Floors < 1 || project.Units < 1)
        {
            problems.Add("quantidades de pavimentos ou unidades inválidas");
        }

        if (project.Disciplines.Count == 0)
        {
            problems.Add("nenhuma disciplina ativa");
        }

        return problems.Count == 0
            ? Passed(
                "PROJECT_DATA",
                "Projeto",
                "Dados cadastrais consistentes",
                $"{project.Floors} pavimento(s), {project.Units} unidade(s) e {project.Disciplines.Count} disciplina(s) ativa(s).")
            : Error(
                "PROJECT_DATA",
                "Projeto",
                "Dados cadastrais precisam de revisão",
                string.Join("; ", problems),
                "Corrija os dados na área Projetos antes de continuar.");
    }

    private static ProjectValidationResult ValidateRules(ProjectWorkspace project)
    {
        try
        {
            var rules = (project.Rules ?? ProjectRules.CreateDefault()).ValidateAndNormalize();
            return Passed(
                "TECHNICAL_RULES",
                "Regras",
                "Regras técnicas válidas",
                $"Unidade {rules.MeasurementUnit}, escala {rules.DrawingScale} e altura padrão de {rules.DefaultFloorHeightMeters:0.00} m.");
        }
        catch (ArgumentException exception)
        {
            return Error(
                "TECHNICAL_RULES",
                "Regras",
                "Regras técnicas inválidas",
                FriendlyMessage(exception),
                "Abra Regras de projeto, corrija os campos e salve novamente.");
        }
    }

    private static ProjectValidationResult ValidateFiles(
        ProjectWorkspace project,
        IReadOnlyList<ProjectFileInspection> inspections)
    {
        if (inspections.Count == 0)
        {
            return Error(
                "FILE_CATALOG",
                "Arquivos",
                "Catálogo sem arquivos",
                "Nenhum arquivo foi catalogado para o projeto.",
                "Catalogue ao menos um desenho DWG, DXF ou IFC antes de iniciar automações.");
        }

        var missingCount = inspections.Count(inspection => !inspection.Exists);
        var changedCount = inspections.Count(inspection => inspection.Exists && inspection.HasChanged);
        var drawingCount = inspections.Count(inspection =>
            inspection.Exists && inspection.File.Kind == ProjectFileKind.Drawing);

        if (missingCount > 0 && (project.Rules?.RequireFileIntegrity ?? true))
        {
            return Error(
                "FILE_CATALOG",
                "Arquivos",
                "Há arquivos obrigatórios ausentes",
                $"{missingCount} referência(s) não foi(ram) encontrada(s) no caminho catalogado.",
                "Restaure os arquivos ou remova as referências inválidas do catálogo.");
        }

        if (drawingCount == 0)
        {
            return Error(
                "FILE_CATALOG",
                "Arquivos",
                "Nenhum desenho técnico disponível",
                "O catálogo não contém um arquivo DWG, DXF ou IFC acessível.",
                "Catalogue o desenho-base que será utilizado nas próximas fases.");
        }

        if (missingCount > 0 || changedCount > 0)
        {
            var details = new List<string>();
            if (missingCount > 0)
            {
                details.Add($"{missingCount} ausente(s)");
            }

            if (changedCount > 0)
            {
                details.Add($"{changedCount} alterado(s) desde a catalogação");
            }

            return Warning(
                "FILE_CATALOG",
                "Arquivos",
                "Catálogo requer conferência",
                string.Join(" e ", details),
                "Revise as referências e atualize os metadados antes de executar automações.");
        }

        return Passed(
            "FILE_CATALOG",
            "Arquivos",
            "Catálogo íntegro",
            $"{inspections.Count} arquivo(s) acessível(is), incluindo {drawingCount} desenho(s) técnico(s).");
    }

    private static ProjectValidationResult ValidateLayers(ProjectWorkspace project)
    {
        if (!(project.Rules?.RequireLayerStandard ?? true))
        {
            return Passed(
                "LAYER_STANDARD",
                "Camadas",
                "Conferência de camadas desativada",
                "O projeto não exige conformidade com o catálogo padrão.");
        }

        var currentKeys = project.Layers
            .Select(layer => layer.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingKeys = DefaultLayerCatalog.Create()
            .Select(layer => layer.Key)
            .Where(key => !currentKeys.Contains(key))
            .ToArray();

        return missingKeys.Length == 0
            ? Passed(
                "LAYER_STANDARD",
                "Camadas",
                "Catálogo de camadas completo",
                $"{project.Layers.Count} camada(s) configurada(s) e nenhuma obrigatória ausente.")
            : Error(
                "LAYER_STANDARD",
                "Camadas",
                "Catálogo de camadas incompleto",
                $"Camadas ausentes: {string.Join(", ", missingKeys)}.",
                "Restaure o catálogo padrão antes de executar automações.");
    }

    private static ProjectValidationResult Passed(
        string code,
        string area,
        string title,
        string detail) =>
        new(code, area, title, detail, "Nenhuma ação necessária.", ProjectValidationStatus.Passed);

    private static ProjectValidationResult Warning(
        string code,
        string area,
        string title,
        string detail,
        string recommendation) =>
        new(code, area, title, detail, recommendation, ProjectValidationStatus.Warning);

    private static ProjectValidationResult Error(
        string code,
        string area,
        string title,
        string detail,
        string recommendation) =>
        new(code, area, title, detail, recommendation, ProjectValidationStatus.Error);

    private static string FriendlyMessage(ArgumentException exception) =>
        exception.Message.Split(" (Parameter", StringSplitOptions.None)[0];
}
