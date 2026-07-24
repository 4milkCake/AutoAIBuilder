using AutoAIBuilder.Application.Navigation;

namespace AutoAIBuilder.Desktop.ViewModels.Modules;

public interface IWorkspaceModule
{
    WorkspaceSection Section { get; }

    string GetActivationMessage();

    void Activate();

    void Refresh();
}
