using AutoAIBuilder.Application.Navigation;

namespace AutoAIBuilder.Desktop.ViewModels.Modules;

public sealed class WorkspaceModule(
    WorkspaceSection section,
    Func<string> activationMessage,
    Action? activate = null,
    Action? refresh = null) : IWorkspaceModule
{
    private readonly Action _activate = activate ?? (() => { });
    private readonly Action _refresh = refresh ?? (() => { });

    public WorkspaceSection Section { get; } = section;

    public string GetActivationMessage() => activationMessage();

    public void Activate() => _activate();

    public void Refresh() => _refresh();
}
