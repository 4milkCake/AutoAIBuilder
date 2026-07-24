using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Application.Settings;
using AutoAIBuilder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class LegacyJsonDataMigratorTests
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
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Run_ImportsAllLegacyDataOnceAndPreservesOriginalFiles()
    {
        var projectsPath = Path.Combine(_testDirectory, "legacy", "projects.json");
        var settingsPath = Path.Combine(_testDirectory, "legacy", "settings.json");
        var activityPath = Path.Combine(_testDirectory, "legacy", "activity.json");
        var databasePath = Path.Combine(_testDirectory, "sqlite", "data.db");

        var workspaceService = new ProjectWorkspaceService(
            new JsonProjectRepository(projectsPath));
        var project = workspaceService.CreateProject(new CreateProjectRequest(
            "Projeto legado",
            "Residencial",
            4,
            16));

        new ApplicationSettingsService(
            new JsonApplicationSettingsRepository(settingsPath))
            .Save(new ApplicationSettings
            {
                DefaultProjectType = "Comercial",
                DefaultFloors = 5,
                DefaultUnits = 10,
                ConfirmFileReferenceRemoval = false
            });

        new ActivityLogService(new JsonActivityLogRepository(activityPath))
            .Record(
                "Sistema",
                "Evento legado",
                "Evento anterior ao SQLite.",
                projectId: project.Id,
                projectName: project.Name);

        var originalProjects = File.ReadAllText(projectsPath);
        var originalSettings = File.ReadAllText(settingsPath);
        var originalActivity = File.ReadAllText(activityPath);
        var database = new SqliteDatabase(databasePath);
        var migrator = new LegacyJsonDataMigrator(
            database,
            projectsPath,
            settingsPath,
            activityPath);

        var firstResult = migrator.Run();
        var secondResult = migrator.Run();

        Assert.AreEqual(1, firstResult.ImportedProjects);
        Assert.IsTrue(firstResult.ImportedSettings);
        Assert.AreEqual(1, firstResult.ImportedActivityEntries);
        Assert.IsFalse(firstResult.HasWarnings);
        Assert.IsFalse(secondResult.ImportedAnything);

        Assert.AreEqual(
            project.Name,
            new SqliteProjectRepository(database).GetById(project.Id)?.Name);
        Assert.AreEqual(
            "Comercial",
            new SqliteApplicationSettingsRepository(database)
                .Load()
                .DefaultProjectType);
        Assert.AreEqual(
            "Evento legado",
            new SqliteActivityLogRepository(database).GetAll().Single().Action);

        Assert.AreEqual(originalProjects, File.ReadAllText(projectsPath));
        Assert.AreEqual(originalSettings, File.ReadAllText(settingsPath));
        Assert.AreEqual(originalActivity, File.ReadAllText(activityPath));
    }

    [TestMethod]
    public void Run_CorruptedLegacyJsonIsPreservedAndRemainsPending()
    {
        var projectsPath = Path.Combine(_testDirectory, "projects.json");
        var originalContent = "{ projeto legado inválido";
        File.WriteAllText(projectsPath, originalContent);
        var database = new SqliteDatabase(
            Path.Combine(_testDirectory, "data.db"));
        var migrator = new LegacyJsonDataMigrator(
            database,
            projectsPath,
            Path.Combine(_testDirectory, "missing-settings.json"),
            Path.Combine(_testDirectory, "missing-activity.json"));

        var result = migrator.Run();

        Assert.AreEqual(0, result.ImportedProjects);
        Assert.IsTrue(result.HasWarnings);
        Assert.AreEqual(originalContent, File.ReadAllText(projectsPath));
        Assert.IsFalse(database.HasDataMigration("legacy-projects-json-v1"));
    }
}
