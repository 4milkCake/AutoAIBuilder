using AutoAIBuilder.Application.History;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void RunProjectValidation()
    {
        if (SelectedProject is null)
        {
            ResetValidationState();
            StatusMessage = "Selecione um projeto ativo antes de executar a validação.";
            return;
        }

        var project = _workspaceService.GetProject(SelectedProject.Id);
        if (project is null)
        {
            ResetValidationState();
            StatusMessage = "O projeto selecionado não foi encontrado.";
            return;
        }

        try
        {
            var report = _validationService.Validate(
                project,
                _workspaceService.InspectFiles(project.Id));

            ReplaceItems(
                ValidationResults,
                report.Results.Select(ProjectValidationItemViewModel.From));

            ValidationPassedCount = report.PassedCount;
            ValidationWarningCount = report.WarningCount;
            ValidationErrorCount = report.ErrorCount;
            ValidationEvaluatedAt = $"Verificado em {report.EvaluatedAt:dd/MM/yyyy HH:mm:ss}";
            ValidationSummary =
                $"{report.Results.Count} verificação(ões): {report.PassedCount} aprovada(s), "
                + $"{report.WarningCount} alerta(s) e {report.ErrorCount} erro(s).";

            if (report.IsAutomationBlocked)
            {
                ValidationGateStatus = "AUTOMAÇÕES BLOQUEADAS";
                ValidationGateAccent = "#FF5D68";
                StatusMessage = "Validação concluída: corrija os erros antes de iniciar automações.";
            }
            else if (report.ErrorCount > 0)
            {
                ValidationGateStatus = "ERROS NÃO BLOQUEANTES";
                ValidationGateAccent = "#F8C33A";
                StatusMessage = "Validação concluída com erros; a regra atual não bloqueia automações.";
            }
            else if (report.WarningCount > 0)
            {
                ValidationGateStatus = "APROVADO COM ALERTAS";
                ValidationGateAccent = "#F8C33A";
                StatusMessage = "Validação concluída: projeto aprovado com pontos de atenção.";
            }
            else
            {
                ValidationGateStatus = "PROJETO APROVADO";
                ValidationGateAccent = "#36D17C";
                StatusMessage = "Validação concluída sem erros ou alertas.";
            }

            TryRecordActivity(
                "Validação",
                "Verificação preventiva",
                $"Verificação concluída com {report.ErrorCount} erro(s), {report.WarningCount} alerta(s) e {report.PassedCount} aprovação(ões).",
                report.ErrorCount > 0
                    ? ActivityLevel.Error
                    : report.WarningCount > 0
                        ? ActivityLevel.Warning
                        : ActivityLevel.Success,
                SelectedProject);
            OnPropertyChanged(nameof(EmptyValidationVisibility));
        }
        catch (InvalidOperationException exception)
        {
            ResetValidationState();
            StatusMessage = exception.Message;
        }
    }

    private void ResetValidationState()
    {
        ValidationResults.Clear();
        ValidationPassedCount = 0;
        ValidationWarningCount = 0;
        ValidationErrorCount = 0;
        ValidationEvaluatedAt = "Ainda não verificado";
        ValidationSummary = SelectedProject is null
            ? "Selecione um projeto ativo para executar a verificação."
            : "Execute a verificação para avaliar o projeto ativo.";
        ValidationGateStatus = "AGUARDANDO VERIFICAÇÃO";
        ValidationGateAccent = "#627087";
        OnPropertyChanged(nameof(EmptyValidationVisibility));
    }
}
