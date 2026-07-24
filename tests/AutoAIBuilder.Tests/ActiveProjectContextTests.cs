using AutoAIBuilder.Application.Projects;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class ActiveProjectContextTests
{
    [TestMethod]
    public void Select_ChangesActiveProjectAndRaisesEventOnce()
    {
        var context = new ActiveProjectContext();
        var projectId = Guid.NewGuid();
        var changeCount = 0;
        context.Changed += (_, _) => changeCount++;

        context.Select(projectId);
        context.Select(projectId);

        Assert.AreEqual(projectId, context.ProjectId);
        Assert.AreEqual(1, changeCount);
    }

    [TestMethod]
    public void Clear_RemovesActiveProject()
    {
        var context = new ActiveProjectContext();
        context.Select(Guid.NewGuid());

        context.Clear();

        Assert.IsNull(context.ProjectId);
    }
}
