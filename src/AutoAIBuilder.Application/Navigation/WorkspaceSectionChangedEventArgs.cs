namespace AutoAIBuilder.Application.Navigation;

public sealed class WorkspaceSectionChangedEventArgs(
    WorkspaceSection previousSection,
    WorkspaceSection currentSection) : EventArgs
{
    public WorkspaceSection PreviousSection { get; } = previousSection;

    public WorkspaceSection CurrentSection { get; } = currentSection;
}
