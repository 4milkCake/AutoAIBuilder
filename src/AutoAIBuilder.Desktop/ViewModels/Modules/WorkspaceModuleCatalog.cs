using AutoAIBuilder.Application.Navigation;

namespace AutoAIBuilder.Desktop.ViewModels.Modules;

public sealed class WorkspaceModuleCatalog(IEnumerable<IWorkspaceModule> modules)
{
    private readonly IReadOnlyDictionary<WorkspaceSection, IWorkspaceModule> _modules =
        modules.ToDictionary(module => module.Section);

    public IWorkspaceModule Get(WorkspaceSection section) =>
        _modules.TryGetValue(section, out var module)
            ? module
            : throw new InvalidOperationException(
                $"Nenhum módulo foi registrado para a seção {section}.");
}
