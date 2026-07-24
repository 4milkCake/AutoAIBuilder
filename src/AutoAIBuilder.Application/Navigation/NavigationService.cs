namespace AutoAIBuilder.Application.Navigation;

public sealed class NavigationService(
    WorkspaceSection initialSection = WorkspaceSection.Dashboard) : INavigationService
{
    public event EventHandler<WorkspaceSectionChangedEventArgs>? SectionChanged;

    public WorkspaceSection CurrentSection { get; private set; } = initialSection;

    public bool NavigateTo(WorkspaceSection section)
    {
        if (!Enum.IsDefined(section))
        {
            throw new ArgumentOutOfRangeException(nameof(section));
        }

        if (CurrentSection == section)
        {
            return false;
        }

        var previousSection = CurrentSection;
        CurrentSection = section;
        SectionChanged?.Invoke(
            this,
            new WorkspaceSectionChangedEventArgs(previousSection, section));
        return true;
    }
}
