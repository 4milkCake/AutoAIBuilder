using System.Globalization;
using System.IO;
using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Navigation;
using AutoAIBuilder.Application.Settings;
using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void LoadProjectRules()
    {
        var project = SelectedProject is null
            ? null
            : _workspaceService.GetProject(SelectedProject.Id);
        var rules = project?.Rules ?? ProjectRules.CreateDefault();

        RuleMeasurementUnit = rules.MeasurementUnit;
        RuleDrawingScale = rules.DrawingScale;
        RuleFloorHeightText = rules.DefaultFloorHeightMeters.ToString("0.00", CultureInfo.CurrentCulture);
        RuleNamingStandard = rules.NamingStandard;
        RuleRequireLayerStandard = rules.RequireLayerStandard;
        RuleRequireFileIntegrity = rules.RequireFileIntegrity;
        RuleBlockAutomationOnErrors = rules.BlockAutomationOnValidationErrors;
        RuleFormError = string.Empty;
    }

    private void SaveProjectRules()
    {
        if (SelectedProject is null)
        {
            SetRuleFormError("Selecione um projeto ativo antes de salvar regras.");
            return;
        }

        var normalizedHeight = RuleFloorHeightText.Replace(',', '.');
        if (!decimal.TryParse(
                normalizedHeight,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var floorHeight))
        {
            SetRuleFormError("Informe uma altura de pavimento válida, por exemplo 2,80.");
            return;
        }

        try
        {
            var updated = _workspaceService.UpdateProjectRules(
                SelectedProject.Id,
                new ProjectRules
                {
                    MeasurementUnit = RuleMeasurementUnit,
                    DrawingScale = RuleDrawingScale,
                    DefaultFloorHeightMeters = floorHeight,
                    NamingStandard = RuleNamingStandard,
                    RequireLayerStandard = RuleRequireLayerStandard,
                    RequireFileIntegrity = RuleRequireFileIntegrity,
                    BlockAutomationOnValidationErrors = RuleBlockAutomationOnErrors
                });

            RefreshProjects(updated.Id);
            Navigate(WorkspaceSection.ProjectRules);
            TryRecordActivity(
                "Regras",
                "Regras atualizadas",
                $"Critérios técnicos do projeto “{updated.Name}” salvos.",
                ActivityLevel.Success,
                SelectedProject);
            StatusMessage = $"Regras do projeto “{updated.Name}” salvas e prontas para validações futuras.";
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            SetRuleFormError(GetFriendlyMessage(exception));
        }
    }

    private void ResetProjectRules()
    {
        if (SelectedProject is null)
        {
            SetRuleFormError("Selecione um projeto ativo antes de restaurar regras.");
            return;
        }

        var defaults = ProjectRules.CreateDefault();
        RuleMeasurementUnit = defaults.MeasurementUnit;
        RuleDrawingScale = defaults.DrawingScale;
        RuleFloorHeightText = defaults.DefaultFloorHeightMeters.ToString("0.00", CultureInfo.CurrentCulture);
        RuleNamingStandard = defaults.NamingStandard;
        RuleRequireLayerStandard = defaults.RequireLayerStandard;
        RuleRequireFileIntegrity = defaults.RequireFileIntegrity;
        RuleBlockAutomationOnErrors = defaults.BlockAutomationOnValidationErrors;
        RuleFormError = string.Empty;
        StatusMessage = "Padrões carregados no formulário; clique em Salvar regras para confirmá-los.";
    }

    private void SaveSettings()
    {
        if (!int.TryParse(SettingsDefaultFloorsText, out var floors) || floors < 1)
        {
            SetSettingsFormError("Informe uma quantidade padrão de pavimentos maior ou igual a 1.");
            return;
        }

        if (!int.TryParse(SettingsDefaultUnitsText, out var units) || units < 1)
        {
            SetSettingsFormError("Informe uma quantidade padrão de unidades maior ou igual a 1.");
            return;
        }

        try
        {
            _applicationSettings = _settingsService.Save(new ApplicationSettings
            {
                DefaultProjectType = SettingsDefaultProjectType,
                DefaultFloors = floors,
                DefaultUnits = units,
                ConfirmFileReferenceRemoval = SettingsConfirmFileReferenceRemoval
            });

            LoadSettingsEditor(_applicationSettings);

            if (_editingProjectId is null)
            {
                ApplyProjectFormDefaults();
            }

            TryRecordActivity(
                "Configurações",
                "Preferências atualizadas",
                "Padrões de novos projetos e confirmações de segurança atualizados.",
                ActivityLevel.Success,
                SelectedProject);
            StatusMessage = "Configurações locais salvas. Os novos padrões já estão ativos.";
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException)
        {
            SetSettingsFormError(GetFriendlyMessage(exception));
        }
    }

    private void ResetSettings()
    {
        _applicationSettings = _settingsService.RestoreDefaults();
        LoadSettingsEditor(_applicationSettings);

        if (_editingProjectId is null)
        {
            ApplyProjectFormDefaults();
        }

        TryRecordActivity(
            "Configurações",
            "Padrões restaurados",
            "Configurações locais restauradas para os valores padrão.",
            ActivityLevel.Information,
            SelectedProject);
        StatusMessage = "Configurações padrão restauradas e salvas.";
    }

    private void LoadSettingsEditor(ApplicationSettings settings)
    {
        SettingsDefaultProjectType = settings.DefaultProjectType;
        SettingsDefaultFloorsText = settings.DefaultFloors.ToString(CultureInfo.InvariantCulture);
        SettingsDefaultUnitsText = settings.DefaultUnits.ToString(CultureInfo.InvariantCulture);
        SettingsConfirmFileReferenceRemoval = settings.ConfirmFileReferenceRemoval;
        SettingsFormError = string.Empty;
    }

    private void ApplyProjectFormDefaults()
    {
        NewProjectType = _applicationSettings.DefaultProjectType;
        NewProjectFloorsText = _applicationSettings.DefaultFloors.ToString(CultureInfo.InvariantCulture);
        NewProjectUnitsText = _applicationSettings.DefaultUnits.ToString(CultureInfo.InvariantCulture);
    }
}
