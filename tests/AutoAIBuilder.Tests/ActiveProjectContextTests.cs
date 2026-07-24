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

    [TestMethod]
    public void PersistentContext_LoadsSavesAndReloadsActiveProject()
    {
        var firstProjectId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();
        var repository = new InMemoryActiveProjectStateRepository(firstProjectId);
        var context = new ActiveProjectContext(repository);

        Assert.AreEqual(firstProjectId, context.ProjectId);

        context.Select(secondProjectId);
        Assert.AreEqual(secondProjectId, repository.ProjectId);

        repository.Save(firstProjectId);
        context.Reload();

        Assert.AreEqual(firstProjectId, context.ProjectId);
    }

    private sealed class InMemoryActiveProjectStateRepository(Guid? projectId)
        : IActiveProjectStateRepository
    {
        public Guid? ProjectId { get; private set; } = projectId;

        public Guid? Load() => ProjectId;

        public void Save(Guid? projectId) => ProjectId = projectId;
    }
}
