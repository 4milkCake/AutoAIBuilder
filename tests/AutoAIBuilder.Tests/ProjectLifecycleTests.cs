using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class ProjectLifecycleTests
{
    [TestMethod]
    public void UpdateDetails_PreservesFilesAndLayers()
    {
        var original = ProjectWorkspace.Create(
                "Projeto original",
                "Residencial",
                floors: 2,
                units: 8)
            .RegisterFile(
                Path.Combine(Path.GetTempPath(), "original.dwg"),
                sizeBytes: 4096);

        var updated = original.UpdateDetails(
            "Projeto atualizado",
            "Comercial",
            floors: 3,
            units: 12);

        Assert.AreEqual(original.Id, updated.Id);
        Assert.AreEqual("Projeto atualizado", updated.Name);
        Assert.AreEqual("Comercial", updated.Type);
        Assert.AreEqual(3, updated.Floors);
        Assert.AreEqual(12, updated.Units);
        Assert.AreEqual(1, updated.Files.Count);
        Assert.AreEqual(original.Layers.Count, updated.Layers.Count);
    }

    [TestMethod]
    public void Duplicate_CreatesIndependentWorkspaceWithoutCopyingSourceFile()
    {
        var original = ProjectWorkspace.Create(
                "Projeto original",
                "Residencial",
                floors: 1,
                units: 1)
            .RegisterFile(
                Path.Combine(Path.GetTempPath(), "planta-base.dwg"),
                sizeBytes: 2048);

        var duplicate = original.Duplicate();

        Assert.AreNotEqual(original.Id, duplicate.Id);
        Assert.AreEqual("Projeto original - Cópia", duplicate.Name);
        Assert.AreEqual(original.Files.Single().SourcePath, duplicate.Files.Single().SourcePath);
        Assert.AreNotEqual(original.Files.Single().Id, duplicate.Files.Single().Id);
        Assert.IsFalse(duplicate.IsArchived);
    }

    [TestMethod]
    public void ArchiveAndRestore_AreReversible()
    {
        var original = ProjectWorkspace.Create(
            "Projeto reversível",
            "Residencial",
            floors: 1,
            units: 1);

        var archived = original.Archive();
        var restored = archived.Restore();

        Assert.IsTrue(archived.IsArchived);
        Assert.IsNotNull(archived.ArchivedAt);
        Assert.IsFalse(restored.IsArchived);
        Assert.IsNull(restored.ArchivedAt);
        Assert.AreEqual(original.Id, restored.Id);
    }

    [TestMethod]
    public void UpdateDetails_RejectsArchivedProject()
    {
        var archived = ProjectWorkspace.Create(
                "Projeto arquivado",
                "Residencial",
                floors: 1,
                units: 1)
            .Archive();

        Assert.ThrowsException<InvalidOperationException>(() =>
            archived.UpdateDetails("Novo nome", "Comercial", 2, 2));
    }
}
