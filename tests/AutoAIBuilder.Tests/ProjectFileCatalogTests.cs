using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Infrastructure.Persistence;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class ProjectFileCatalogTests
{
    private string _testDirectory = null!;
    private ProjectWorkspaceService _service = null!;
    private Guid _projectId;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.FileCatalog.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        var repository = new JsonProjectRepository(
            Path.Combine(_testDirectory, "data", "projects.json"));
        _service = new ProjectWorkspaceService(repository);
        _projectId = _service.CreateProject(new CreateProjectRequest(
            "Projeto de arquivos",
            "Residencial",
            1,
            1)).Id;
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
    public void RegisterFiles_CatalogsMultipleFilesWithoutDuplicates()
    {
        var drawing = CreateFile("planta.dwg", "conteúdo dwg");
        var document = CreateFile("memorial.pdf", "conteúdo pdf");

        var updated = _service.RegisterFiles(
            _projectId,
            [drawing, document, drawing.ToUpperInvariant()]);

        Assert.AreEqual(2, updated.Files.Count);
        Assert.IsTrue(updated.Files.All(file => file.LastKnownWriteTime is not null));
        Assert.IsTrue(File.Exists(drawing));
        Assert.IsTrue(File.Exists(document));
    }

    [TestMethod]
    public void InspectAndRefresh_DetectsChangedAndMissingFiles()
    {
        var changedPath = CreateFile("alterado.dwg", "versão 1");
        var missingPath = CreateFile("ausente.pdf", "temporário");
        var project = _service.RegisterFiles(_projectId, [changedPath, missingPath]);
        var changedId = project.Files.Single(file => file.SourcePath == changedPath).Id;

        File.AppendAllText(changedPath, " com alteração maior");
        File.Delete(missingPath);

        var inspection = _service.InspectFiles(_projectId);

        Assert.IsTrue(inspection.Single(item => item.File.Id == changedId).HasChanged);
        Assert.IsFalse(inspection.Single(item => item.File.SourcePath == missingPath).Exists);

        _service.RefreshFileMetadata(_projectId, changedId);
        var refreshed = _service.InspectFiles(_projectId);

        Assert.IsFalse(refreshed.Single(item => item.File.Id == changedId).HasChanged);
    }

    [TestMethod]
    public void RemoveFileReference_KeepsOriginalFileUntouched()
    {
        var sourcePath = CreateFile("manter.xlsx", "planilha");
        var project = _service.RegisterFile(_projectId, sourcePath);
        var fileId = project.Files.Single().Id;

        var updated = _service.RemoveFileReference(_projectId, fileId);

        Assert.AreEqual(0, updated.Files.Count);
        Assert.IsTrue(File.Exists(sourcePath));
    }

    private string CreateFile(string name, string contents)
    {
        var path = Path.Combine(_testDirectory, name);
        File.WriteAllText(path, contents);
        return Path.GetFullPath(path);
    }
}
