using AutoAIBuilder.Application.Navigation;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class NavigationServiceTests
{
    [TestMethod]
    public void NavigateTo_ChangesSectionAndRaisesOneEvent()
    {
        var service = new NavigationService();
        WorkspaceSectionChangedEventArgs? received = null;
        service.SectionChanged += (_, eventArgs) => received = eventArgs;

        var changed = service.NavigateTo(WorkspaceSection.Files);

        Assert.IsTrue(changed);
        Assert.AreEqual(WorkspaceSection.Files, service.CurrentSection);
        Assert.IsNotNull(received);
        Assert.AreEqual(WorkspaceSection.Dashboard, received.PreviousSection);
        Assert.AreEqual(WorkspaceSection.Files, received.CurrentSection);
    }

    [TestMethod]
    public void NavigateTo_SameSectionDoesNotRaiseDuplicateEvent()
    {
        var service = new NavigationService(WorkspaceSection.Projects);
        var eventCount = 0;
        service.SectionChanged += (_, _) => eventCount++;

        var changed = service.NavigateTo(WorkspaceSection.Projects);

        Assert.IsFalse(changed);
        Assert.AreEqual(0, eventCount);
    }
}
