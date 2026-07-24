namespace AutoAIBuilder.Application.Navigation;

public interface INavigationService
{
    event EventHandler<WorkspaceSectionChangedEventArgs>? SectionChanged;

    WorkspaceSection CurrentSection { get; }

    bool NavigateTo(WorkspaceSection section);
}
