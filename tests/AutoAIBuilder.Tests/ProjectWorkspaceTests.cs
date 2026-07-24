using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class ProjectWorkspaceTests
{
    [TestMethod]
    public void Create_AddsDefaultDisciplinesAndLayers()
    {
        var project = ProjectWorkspace.Create(
            "Edifício Central",
            "Residencial",
            floors: 8,
            units: 32,
            now: new DateTimeOffset(2026, 7, 24, 10, 0, 0, TimeSpan.FromHours(-3)));

        Assert.AreEqual("Edifício Central", project.Name);
        CollectionAssert.AreEquivalent(
            new[] { "Elétrico", "Hidrossanitário" },
            project.Disciplines.ToArray());
        Assert.AreEqual(11, project.Layers.Count);
        Assert.IsTrue(project.Layers.Any(layer => layer.Key == "ELE-PONTOS"));
        Assert.IsTrue(project.Layers.Any(layer => layer.Key == "HID-AGUA"));
    }

    [TestMethod]
    public void Create_RejectsBlankName()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            ProjectWorkspace.Create(" ", "Residencial", 1, 1));
    }

    [TestMethod]
    public void RegisterFile_CatalogsMetadataWithoutDuplicatingPath()
    {
        var timestamp = new DateTimeOffset(2026, 7, 24, 11, 0, 0, TimeSpan.FromHours(-3));
        var project = ProjectWorkspace.Create(
            "Projeto teste",
            "Comercial",
            2,
            4,
            now: timestamp);

        var sourcePath = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder",
            "planta-tipo.dwg");

        var firstRegistration = project.RegisterFile(sourcePath, 1_500_000, timestamp.AddMinutes(1));
        var duplicatedRegistration = firstRegistration.RegisterFile(
            sourcePath.ToUpperInvariant(),
            1_500_000,
            timestamp.AddMinutes(2));

        Assert.AreEqual(1, firstRegistration.Files.Count);
        Assert.AreEqual(ProjectFileKind.Drawing, firstRegistration.Files[0].Kind);
        Assert.AreEqual(1, duplicatedRegistration.Files.Count);
    }
}
