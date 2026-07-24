using AutoAIBuilder.Application.Dashboard;
using AutoAIBuilder.Domain.Automation;
using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Infrastructure.Dashboard;

public sealed class ProjectDashboardProvider : IDashboardProvider
{
    public DashboardSnapshot GetFor(ProjectWorkspace project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var hasFiles = project.Files.Count > 0;

        WorkflowStep[] workflow =
        [
            new(1, "Importar", hasFiles ? WorkflowState.Completed : WorkflowState.Pending),
            new(2, "Máscara", WorkflowState.Pending),
            new(3, "Identificação", WorkflowState.Pending),
            new(4, "Regras", WorkflowState.Pending),
            new(5, "Proposição", WorkflowState.Pending),
            new(6, "Lançamento", WorkflowState.Pending),
            new(7, "Validação", WorkflowState.Pending),
            new(8, "Relatórios", WorkflowState.Pending)
        ];

        DashboardAgent[] agents =
        [
            new("Agente de Máscaras", "Preparação de camadas", WorkflowState.Pending, 0),
            new("Agente de Identificação", "Leitura de ambientes", WorkflowState.Pending, 0),
            new("Agente Elétrico", "Proposição de pontos", WorkflowState.Pending, 0),
            new("Agente Hidrossanitário", "Traçado de redes", WorkflowState.Pending, 0),
            new("Agente de Validação", "Conferência normativa", WorkflowState.Pending, 0)
        ];

        DashboardMetric[] metrics =
        [
            new("Arquivos catalogados", project.Files.Count.ToString(), hasFiles ? 100 : 0, "#36D17C"),
            new("Camadas configuradas", project.Layers.Count.ToString(), project.Layers.Count > 0 ? 100 : 0, "#F8C33A"),
            new("Disciplinas ativas", project.Disciplines.Count.ToString(), project.Disciplines.Count > 0 ? 100 : 0, "#4F8CFF"),
            new("Pavimentos", project.Floors.ToString(), 100, "#28C8D8"),
            new("Unidades", project.Units.ToString(), 100, "#A970FF")
        ];

        var activities = new List<DashboardActivity>
        {
            new(project.UpdatedAt, "Dados do projeto carregados", "#2C9BFF"),
            new(project.CreatedAt, "Projeto criado", "#36D17C")
        };

        if (hasFiles)
        {
            activities.Insert(
                0,
                new(
                    project.Files.Max(file => file.ImportedAt),
                    $"{project.Files.Count} arquivo(s) disponível(is) no catálogo",
                    "#F8C33A"));
        }

        return new DashboardSnapshot(
            project.ToSummary(),
            workflow,
            agents,
            metrics,
            activities,
            hasFiles
                ? "Revisar o catálogo de arquivos antes de configurar as máscaras."
                : "Catalogar o primeiro arquivo do projeto.");
    }
}
