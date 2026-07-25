using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.Navigation;
using AutoAIBuilder.Application.Notifications;
using AutoAIBuilder.Desktop.ViewModels.Modules;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void RefreshCurrentSection()
    {
        _moduleCatalog.Get(CurrentSection).Refresh();
    }

    private WorkspaceModuleCatalog CreateModuleCatalog() => new(
    [
        new WorkspaceModule(
            WorkspaceSection.Dashboard,
            () => "Painel do projeto ativo carregado.",
            refresh: () =>
            {
                RefreshActiveProject();
                StatusMessage = "Painel atualizado com os dados locais mais recentes.";
            }),
        new WorkspaceModule(
            WorkspaceSection.Projects,
            () => "Pesquise, edite, duplique ou arquive projetos locais.",
            refresh: () =>
            {
                RefreshProjects(SelectedProject?.Id);
                StatusMessage = "Lista de projetos atualizada.";
            }),
        new WorkspaceModule(
            WorkspaceSection.Files,
            () => "Catálogo pronto para registrar arquivos sem alterar os originais.",
            refresh: () =>
            {
                RefreshProjectFiles();
                StatusMessage = "Catálogo e integridade dos arquivos atualizados.";
            }),
        new WorkspaceModule(
            WorkspaceSection.Masks,
            () => "Registro interno carregado; máscaras catalogadas continuam "
                  + "sem execução no Marco 11.6B.",
            activate: RefreshMaskCatalog,
            refresh: () =>
            {
                RefreshMaskCatalog();
                StatusMessage =
                    "Catálogo, adaptadores internos e avaliações atualizados.";
            }),
        new WorkspaceModule(
            WorkspaceSection.Automation,
            () => SelectedProject is null
                ? "Selecione um projeto ativo para usar o piloto seguro."
                : "Piloto de cópia técnica pronto para simulação.",
            activate: RefreshAutomationPilot,
            refresh: () =>
            {
                RefreshProjectFiles();
                RefreshAutomationPilot();
                StatusMessage =
                    "Entradas e estado do piloto seguro foram atualizados.";
            }),
        new WorkspaceModule(
            WorkspaceSection.ProjectRules,
            () => SelectedProject is null
                ? "Selecione um projeto ativo para configurar suas regras."
                : $"Regras do projeto “{SelectedProject.Name}” carregadas.",
            refresh: () =>
            {
                LoadProjectRules();
                StatusMessage = "Regras do projeto recarregadas.";
            }),
        new WorkspaceModule(
            WorkspaceSection.Validators,
            () => SelectedProject is null
                ? "Selecione um projeto ativo para executar a validação."
                : $"Validadores prontos para verificar o projeto “{SelectedProject.Name}”.",
            activate: () =>
            {
                if (SelectedProject is not null)
                {
                    RunProjectValidation();
                }
            },
            refresh: RunProjectValidation),
        new WorkspaceModule(
            WorkspaceSection.Reports,
            () => SelectedProject is null
                ? "Selecione um projeto ativo para gerar o relatório."
                : $"Relatório de prontidão do projeto “{SelectedProject.Name}” carregado.",
            activate: () =>
            {
                if (SelectedProject is not null)
                {
                    GenerateProjectReport();
                }
            },
            refresh: GenerateProjectReport),
        new WorkspaceModule(
            WorkspaceSection.History,
            () => "Histórico local de ações carregado.",
            activate: RefreshHistory,
            refresh: () =>
            {
                RefreshHistory();
                StatusMessage = "Histórico local atualizado.";
            }),
        new WorkspaceModule(
            WorkspaceSection.Diagnostics,
            () => "Diagnóstico técnico e logs estruturados carregados.",
            activate: RefreshDiagnostics,
            refresh: RefreshDiagnostics),
        new WorkspaceModule(
            WorkspaceSection.Settings,
            () => "Preferências locais do AutoAIBuilder carregadas.",
            refresh: () =>
            {
                LoadSettingsEditor(_applicationSettings);
                StatusMessage = "Configurações locais recarregadas.";
            })
    ]);

    private void OnSectionChanged(
        object? sender,
        WorkspaceSectionChangedEventArgs eventArgs) =>
        ActivateSection(eventArgs.CurrentSection);

    private void ActivateSection(WorkspaceSection section)
    {
        CurrentSection = section;

        foreach (var item in Navigation)
        {
            item.IsActive = item.Section == section;
        }

        var module = _moduleCatalog.Get(section);
        module.Activate();
        StatusMessage = module.GetActivationMessage();
    }

    private void OnNotificationPublished(
        object? sender,
        AppNotification notification)
    {
        if (string.IsNullOrWhiteSpace(notification.Message))
        {
            if (_isNotificationVisible)
            {
                _isNotificationVisible = false;
                OnPropertyChanged(nameof(NotificationVisibility));
            }

            return;
        }

        NotificationMessage = notification.Message;

        if (notification.Tone is NotificationTone.Warning or NotificationTone.Error)
        {
            TryWriteDiagnostic(
                notification.Tone == NotificationTone.Error
                    ? DiagnosticLevel.Error
                    : DiagnosticLevel.Warning,
                "Notification",
                notification.Message);
        }

        (NotificationTitle, NotificationIcon, NotificationAccent, NotificationBackground) =
            notification.Tone switch
            {
                NotificationTone.Success => ("Concluído", "✓", "#36D17C", "#102B22"),
                NotificationTone.Warning => ("Atenção", "!", "#F8C33A", "#332A12"),
                NotificationTone.Error => ("Não foi possível concluir", "×", "#FF5D68", "#351A23"),
                _ => ("Informação", "i", "#2C9BFF", "#102641")
            };

        if (!_isNotificationVisible)
        {
            _isNotificationVisible = true;
            OnPropertyChanged(nameof(NotificationVisibility));
        }
    }

    private void DismissNotification()
    {
        _notificationService.Dismiss();
    }

    private void Navigate(WorkspaceSection? section)
    {
        if (section is null)
        {
            StatusMessage = "Este módulo ainda não foi implementado. Nenhuma automação foi executada.";
            return;
        }

        if (!_navigationService.NavigateTo(section.Value))
        {
            ActivateSection(section.Value);
        }
    }
}
