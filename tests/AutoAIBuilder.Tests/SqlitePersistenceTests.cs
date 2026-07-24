using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Application.Settings;
using AutoAIBuilder.Domain.Projects;
using AutoAIBuilder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class SqlitePersistenceTests
{
    private string _testDirectory = null!;
    private string _databasePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.Tests",
            Guid.NewGuid().ToString("N"));
        _databasePath = Path.Combine(_testDirectory, "data", "autoaibuilder.db");
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
    public void Repositories_RoundTripProjectsSettingsHistoryAndActiveProject()
    {
        var database = new SqliteDatabase(_databasePath);
        var projectRepository = new SqliteProjectRepository(database);
        var workspaceService = new ProjectWorkspaceService(projectRepository);
        var project = workspaceService.CreateProject(new CreateProjectRequest(
            "Edifício SQLite",
            "Comercial",
            8,
            24));

        new ApplicationSettingsService(
            new SqliteApplicationSettingsRepository(database))
            .Save(new ApplicationSettings
            {
                DefaultProjectType = "Industrial",
                DefaultFloors = 2,
                DefaultUnits = 3,
                ConfirmFileReferenceRemoval = false
            });

        new ActivityLogService(new SqliteActivityLogRepository(database))
            .Record(
                "Projetos",
                "Projeto persistido",
                "Teste SQLite.",
                ActivityLevel.Success,
                project.Id,
                project.Name);

        var activeRepository =
            new SqliteActiveProjectStateRepository(database);
        activeRepository.Save(project.Id);

        var reopened = new SqliteDatabase(_databasePath);

        Assert.AreEqual(1, reopened.GetSchemaVersion());
        Assert.AreEqual("ok", reopened.QuickCheck());
        Assert.AreEqual(
            project.Name,
            new SqliteProjectRepository(reopened).GetById(project.Id)?.Name);
        Assert.AreEqual(
            "Industrial",
            new SqliteApplicationSettingsRepository(reopened)
                .Load()
                .DefaultProjectType);
        Assert.AreEqual(
            "Projeto persistido",
            new SqliteActivityLogRepository(reopened).GetAll().Single().Action);
        Assert.AreEqual(
            project.Id,
            new SqliteActiveProjectStateRepository(reopened).Load());
    }

    [TestMethod]
    public void SeparateDatabaseInstances_SerializeConcurrentWritesWithoutLoss()
    {
        var firstRepository = new SqliteProjectRepository(
            new SqliteDatabase(_databasePath));
        var secondRepository = new SqliteProjectRepository(
            new SqliteDatabase(_databasePath));

        Parallel.For(
            0,
            24,
            index =>
            {
                var project = ProjectWorkspace.Create(
                    $"Projeto {index:00}",
                    "Residencial",
                    1,
                    1);
                (index % 2 == 0 ? firstRepository : secondRepository)
                    .Save(project);
            });

        var projects = new SqliteProjectRepository(
                new SqliteDatabase(_databasePath))
            .GetAll();

        Assert.AreEqual(24, projects.Count);
        Assert.AreEqual(24, projects.Select(project => project.Id).Distinct().Count());
    }

    [TestMethod]
    public void CorruptedProjectPayload_IsPreservedAndBlocksFurtherWrites()
    {
        var database = new SqliteDatabase(_databasePath);
        database.Initialize();

        using (var connection = database.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                INSERT INTO Projects (Id, PayloadJson, UpdatedAt)
                VALUES ($id, '{ projeto inválido', $updatedAt);
                """;
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            command.Parameters.AddWithValue(
                "$updatedAt",
                DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }

        var repository = new SqliteProjectRepository(database);

        Assert.AreEqual(0, repository.GetAll().Count);
        Assert.ThrowsException<InvalidDataException>(() =>
            repository.Save(ProjectWorkspace.Create(
                "Não sobrescrever",
                "Residencial",
                1,
                1)));

        using var verificationConnection = database.OpenConnection();
        using var verificationCommand = verificationConnection.CreateCommand();
        verificationCommand.CommandText =
            "SELECT PayloadJson FROM Projects LIMIT 1;";
        Assert.AreEqual(
            "{ projeto inválido",
            verificationCommand.ExecuteScalar() as string);
    }
}
