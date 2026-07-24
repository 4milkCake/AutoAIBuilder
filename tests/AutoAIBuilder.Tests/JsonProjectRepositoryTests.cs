using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Domain.Projects;
using AutoAIBuilder.Infrastructure.Persistence;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class JsonProjectRepositoryTests
{
    private string _testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Save_RoundTripsProjectAndLayers()
    {
        var repository = CreateRepository();
        var service = new ProjectWorkspaceService(repository);

        var created = service.CreateProject(new CreateProjectRequest(
            "Centro Empresarial",
            "Comercial",
            5,
            20));
        service.UpdateProjectRules(created.Id, new ProjectRules
        {
            DrawingScale = "1:100",
            DefaultFloorHeightMeters = 3.25m
        });

        var loaded = repository.GetById(created.Id);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(created.Name, loaded.Name);
        Assert.AreEqual(11, loaded.Layers.Count);
        Assert.AreEqual("1:100", loaded.Rules.DrawingScale);
        Assert.AreEqual(3.25m, loaded.Rules.DefaultFloorHeightMeters);
        Assert.AreEqual(1, repository.GetAll().Count);
    }

    [TestMethod]
    public void RegisterFile_PersistsOnlyMetadataAndKeepsSourceUntouched()
    {
        var repository = CreateRepository();
        var service = new ProjectWorkspaceService(repository);
        var project = service.CreateProject(new CreateProjectRequest(
            "Galpão industrial",
            "Industrial",
            1,
            1));

        var sourcePath = Path.Combine(_testDirectory, "levantamento.pdf");
        File.WriteAllText(sourcePath, "conteúdo de teste");

        var updated = service.RegisterFile(project.Id, sourcePath);
        var reloaded = repository.GetById(project.Id);

        Assert.AreEqual(1, updated.Files.Count);
        Assert.IsTrue(File.Exists(sourcePath));
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(Path.GetFullPath(sourcePath), reloaded.Files.Single().SourcePath);
    }

    [TestMethod]
    public void ProjectLifecycle_PersistsArchiveRestoreAndUpdatedDetails()
    {
        var repository = CreateRepository();
        var service = new ProjectWorkspaceService(repository);
        var project = service.CreateProject(new CreateProjectRequest(
            "Projeto inicial",
            "Residencial",
            1,
            1));

        var updated = service.UpdateProject(new UpdateProjectRequest(
            project.Id,
            "Projeto revisado",
            "Comercial",
            3,
            10));
        var archived = service.ArchiveProject(updated.Id);
        var restored = service.RestoreProject(archived.Id);

        var reloaded = repository.GetById(project.Id);

        Assert.IsNotNull(reloaded);
        Assert.AreEqual("Projeto revisado", reloaded.Name);
        Assert.AreEqual(3, reloaded.Floors);
        Assert.IsFalse(reloaded.IsArchived);
        Assert.IsNull(reloaded.ArchivedAt);
        Assert.AreEqual(restored.Id, reloaded.Id);
    }

    [TestMethod]
    public void Load_OldProjectWithoutRulesReceivesSafeDefaults()
    {
        var path = Path.Combine(_testDirectory, "data", "projects.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var project = ProjectWorkspace.Create(
            "Projeto legado",
            "Residencial",
            2,
            4);
        var json = JsonSerializer.Serialize(
            new[] { project },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var document = JsonNode.Parse(json)!.AsArray();
        document[0]!.AsObject().Remove("rules");
        File.WriteAllText(path, document.ToJsonString());

        var loaded = new JsonProjectRepository(path).GetById(project.Id);

        Assert.IsNotNull(loaded);
        Assert.IsNotNull(loaded.Rules);
        Assert.AreEqual("Milímetros", loaded.Rules.MeasurementUnit);
        Assert.IsTrue(loaded.Rules.BlockAutomationOnValidationErrors);
    }

    [TestMethod]
    public void CorruptedFile_IsPreservedAndBlocksOverwrite()
    {
        var path = Path.Combine(_testDirectory, "data", "projects.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const string corruptedContent = "{ projeto inválido";
        File.WriteAllText(path, corruptedContent);
        var repository = new JsonProjectRepository(path);

        var projects = repository.GetAll();
        var exception = Assert.ThrowsException<InvalidDataException>(
            () => repository.Save(ProjectWorkspace.Create(
                "Não deve sobrescrever",
                "Residencial",
                1,
                1)));

        Assert.AreEqual(0, projects.Count);
        StringAssert.Contains(exception.Message, "preservado");
        Assert.AreEqual(corruptedContent, File.ReadAllText(path));
    }

    [TestMethod]
    public void MissingFile_ReturnsEmptyCollectionWithoutCreatingStorage()
    {
        var path = Path.Combine(_testDirectory, "data", "projects.json");
        var repository = new JsonProjectRepository(path);

        var projects = repository.GetAll();

        Assert.AreEqual(0, projects.Count);
        Assert.IsFalse(File.Exists(path));
    }

    private JsonProjectRepository CreateRepository() =>
        new(Path.Combine(_testDirectory, "data", "projects.json"));
}
