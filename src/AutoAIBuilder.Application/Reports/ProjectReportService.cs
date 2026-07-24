using System.Text;
using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Application.Validation;
using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Application.Reports;

public sealed class ProjectReportService
{
    public ProjectReadinessReport Build(
        ProjectWorkspace project,
        IReadOnlyList<ProjectFileInspection> fileInspections,
        ProjectValidationReport validation,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(fileInspections);
        ArgumentNullException.ThrowIfNull(validation);

        if (project.Id != validation.ProjectId)
        {
            throw new ArgumentException(
                "O relatório de validação não pertence ao projeto informado.",
                nameof(validation));
        }

        var generatedAt = now ?? DateTimeOffset.Now;
        var rules = project.Rules ?? ProjectRules.CreateDefault();
        var builder = new StringBuilder();

        builder.AppendLine("AUTOAIBUILDER — RELATÓRIO DE PRONTIDÃO");
        builder.AppendLine(new string('=', 48));
        builder.AppendLine($"Projeto: {project.Name}");
        builder.AppendLine($"Tipo: {project.Type}");
        builder.AppendLine($"Gerado em: {generatedAt:dd/MM/yyyy HH:mm:ss zzz}");
        builder.AppendLine($"Pavimentos: {project.Floors}");
        builder.AppendLine($"Unidades: {project.Units}");
        builder.AppendLine($"Disciplinas: {string.Join(", ", project.Disciplines)}");
        builder.AppendLine();
        builder.AppendLine("REGRAS TÉCNICAS");
        builder.AppendLine($"- Unidade: {rules.MeasurementUnit}");
        builder.AppendLine($"- Escala: {rules.DrawingScale}");
        builder.AppendLine($"- Altura padrão do pavimento: {rules.DefaultFloorHeightMeters:0.00} m");
        builder.AppendLine($"- Nomenclatura: {rules.NamingStandard}");
        builder.AppendLine($"- Exigir camadas padrão: {YesNo(rules.RequireLayerStandard)}");
        builder.AppendLine($"- Exigir integridade de arquivos: {YesNo(rules.RequireFileIntegrity)}");
        builder.AppendLine($"- Bloquear automações com erros: {YesNo(rules.BlockAutomationOnValidationErrors)}");
        builder.AppendLine();
        builder.AppendLine("ARQUIVOS CATALOGADOS");

        if (fileInspections.Count == 0)
        {
            builder.AppendLine("- Nenhum arquivo catalogado.");
        }
        else
        {
            foreach (var inspection in fileInspections.OrderBy(item => item.File.Name))
            {
                var status = !inspection.Exists
                    ? "AUSENTE"
                    : inspection.HasChanged
                        ? "ALTERADO"
                        : "ÍNTEGRO";
                builder.AppendLine(
                    $"- [{status}] {inspection.File.Name} | {inspection.File.Kind} | {inspection.File.SourcePath}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("RESULTADO DAS VERIFICAÇÕES");
        foreach (var result in validation.Results)
        {
            builder.AppendLine(
                $"- [{StatusLabel(result.Status)}] {result.Area} — {result.Title}");
            builder.AppendLine($"  Detalhe: {result.Detail}");
            builder.AppendLine($"  Recomendação: {result.Recommendation}");
        }

        builder.AppendLine();
        builder.AppendLine("RESUMO");
        builder.AppendLine($"- Aprovados: {validation.PassedCount}");
        builder.AppendLine($"- Alertas: {validation.WarningCount}");
        builder.AppendLine($"- Erros: {validation.ErrorCount}");
        builder.AppendLine(
            $"- Prontidão: {(validation.IsAutomationBlocked ? "AUTOMAÇÕES BLOQUEADAS" : "APTO SEGUNDO AS REGRAS ATUAIS")}");
        builder.AppendLine();
        builder.AppendLine("Este relatório é diagnóstico e não executa nem modifica automações ou arquivos catalogados.");

        var fileNamePrefix = $"{SanitizeFileName(project.Name)}-prontidao-{generatedAt:yyyyMMdd-HHmm}";

        return new ProjectReadinessReport(
            project.Id,
            project.Name,
            generatedAt,
            $"{fileNamePrefix}.txt",
            $"{fileNamePrefix}.csv",
            $"{fileNamePrefix}.pdf",
            builder.ToString(),
            BuildCsv(project, validation),
            validation.PassedCount,
            validation.WarningCount,
            validation.ErrorCount,
            validation.IsAutomationBlocked);
    }

    private static string BuildCsv(
        ProjectWorkspace project,
        ProjectValidationReport validation)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Projeto;Código;Área;Status;Título;Detalhe;Recomendação");

        foreach (var result in validation.Results)
        {
            builder.AppendLine(string.Join(
                ";",
                Csv(project.Name),
                Csv(result.Code),
                Csv(result.Area),
                Csv(StatusLabel(result.Status)),
                Csv(result.Title),
                Csv(result.Detail),
                Csv(result.Recommendation)));
        }

        return builder.ToString();
    }

    private static string Csv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private static string YesNo(bool value) => value ? "Sim" : "Não";

    private static string StatusLabel(ProjectValidationStatus status) => status switch
    {
        ProjectValidationStatus.Passed => "APROVADO",
        ProjectValidationStatus.Warning => "ALERTA",
        _ => "ERRO"
    };

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(
            value.Trim()
                .Select(character => invalidCharacters.Contains(character) ? '-' : character)
                .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "projeto" : sanitized;
    }
}
