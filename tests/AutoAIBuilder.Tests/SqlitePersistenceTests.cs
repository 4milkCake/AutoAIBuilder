using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Operations;
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
        var execution = new OperationExecution(
            Guid.NewGuid(),
            "data-backup",
            "Criar backup",
            "local-data-store",
            project.Id,
            OperationExecutionStatus.Succeeded,
            100,
            "Operação concluída",
            DateTimeOffset.UtcNow.AddSeconds(-2),
            DateTimeOffset.UtcNow.AddSeconds(-1),
            DateTimeOffset.UtcNow,
            300,
            null,
            null);
        new SqliteOperationExecutionRepository(database).Save(execution);

        var reopened = new SqliteDatabase(_databasePath);

        Assert.AreEqual(
            SqliteDatabase.CurrentSchemaVersion,
            reopened.GetSchemaVersion());
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
        Assert.AreEqual(
            execution,
            new SqliteOperationExecutionRepository(reopened).Get(execution.Id));
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

    [TestMethod]
    public void Initialize_UpgradesVersionOneDatabaseToOperationalSchema()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        using (var connection = new SqliteConnection(
                   $"Data Source={_databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE SchemaMigrations (
                    Version INTEGER NOT NULL PRIMARY KEY,
                    AppliedAt TEXT NOT NULL,
                    Description TEXT NOT NULL
                );
                INSERT INTO SchemaMigrations (Version, AppliedAt, Description)
                VALUES (1, '2026-01-01T00:00:00.0000000+00:00', 'Teste');
                PRAGMA user_version = 1;
                """;
            command.ExecuteNonQuery();
        }

        var database = new SqliteDatabase(_databasePath);
        database.Initialize();

        Assert.AreEqual(SqliteDatabase.CurrentSchemaVersion, database.GetSchemaVersion());
        using var verification = database.OpenConnection();
        using var tableCommand = verification.CreateCommand();
        tableCommand.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = 'OperationExecutions';
            """;
        Assert.AreEqual(1L, tableCommand.ExecuteScalar());

        using var auditCommand = verification.CreateCommand();
        auditCommand.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = 'AutomationAudits';
            """;
        Assert.AreEqual(1L, auditCommand.ExecuteScalar());

        using var catalogCommand = verification.CreateCommand();
        catalogCommand.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = 'AutomationMaskCatalog';
            """;
        Assert.AreEqual(1L, catalogCommand.ExecuteScalar());
    }

    [TestMethod]
    public void Initialize_UpgradesVersionThreeDatabaseToMaskCatalogSchema()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        using (var connection = new SqliteConnection(
                   $"Data Source={_databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE SchemaMigrations (
                    Version INTEGER NOT NULL PRIMARY KEY,
                    AppliedAt TEXT NOT NULL,
                    Description TEXT NOT NULL
                );
                INSERT INTO SchemaMigrations (Version, AppliedAt, Description)
                VALUES (3, '2026-01-01T00:00:00.0000000+00:00', 'Teste');
                PRAGMA user_version = 3;
                """;
            command.ExecuteNonQuery();
        }

        var database = new SqliteDatabase(_databasePath);
        database.Initialize();

        Assert.AreEqual(4, database.GetSchemaVersion());
        using var verification = database.OpenConnection();
        using var commandVerification = verification.CreateCommand();
        commandVerification.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = 'AutomationMaskCatalog';
            """;
        Assert.AreEqual(1L, commandVerification.ExecuteScalar());
    }

    [TestMethod]
    public void AutomationAuditRepository_RoundTripsAndRecoversRunningEntries()
    {
        var database = new SqliteDatabase(_databasePath);
        var repository = new SqliteAutomationAuditRepository(database);
        var now = DateTimeOffset.UtcNow;
        var input = new AutomationInputSnapshot(
            @"D:\entrada.dwg",
            42,
            now.AddMinutes(-1),
            new string('A', 64));
        var running = new AutomationAuditEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "mascara-segura",
            "1.0.0",
            AutomationExecutionMode.Apply,
            AutomationAuditStatus.Running,
            new string('B', 64),
            [input],
            [],
            null,
            null,
            "Em execução.",
            now,
            null);
        repository.Save(running);

        var loaded = repository.Get(running.Id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(running.Id, loaded.Id);
        Assert.AreEqual(running.Status, loaded.Status);
        Assert.AreEqual(running.IdempotencyKey, loaded.IdempotencyKey);
        Assert.AreEqual(input, loaded.Inputs.Single());
        Assert.AreEqual(0, loaded.OutputPaths.Count);
        Assert.AreEqual(
            1,
            repository.MarkIncompleteAsInterrupted(
                now.AddMinutes(1),
                "Processo encerrado."));
        Assert.AreEqual(
            AutomationAuditStatus.Interrupted,
            repository.Get(running.Id)?.Status);
    }

    [TestMethod]
    public void OperationRepository_RecoversIncompleteExecutions()
    {
        var database = new SqliteDatabase(_databasePath);
        var repository = new SqliteOperationExecutionRepository(database);
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var pending = CreateOperation(
            "pending",
            OperationExecutionStatus.Pending,
            createdAt);
        var running = CreateOperation(
            "running",
            OperationExecutionStatus.Running,
            createdAt.AddSeconds(1));
        var completed = CreateOperation(
            "completed",
            OperationExecutionStatus.Succeeded,
            createdAt.AddSeconds(2));
        repository.Save(pending);
        repository.Save(running);
        repository.Save(completed);

        var recovered = repository.MarkIncompleteAsInterrupted(
            DateTimeOffset.UtcNow,
            "Encerramento anterior.");

        Assert.AreEqual(2, recovered);
        Assert.AreEqual(
            OperationExecutionStatus.Interrupted,
            repository.Get(pending.Id)?.Status);
        Assert.AreEqual(
            OperationExecutionStatus.Interrupted,
            repository.Get(running.Id)?.Status);
        Assert.AreEqual(
            OperationExecutionStatus.Succeeded,
            repository.Get(completed.Id)?.Status);
    }

    private static OperationExecution CreateOperation(
        string type,
        OperationExecutionStatus status,
        DateTimeOffset createdAt) =>
        new(
            Guid.NewGuid(),
            type,
            type,
            "test-resource",
            null,
            status,
            status == OperationExecutionStatus.Succeeded ? 100 : 20,
            "Teste",
            createdAt,
            status == OperationExecutionStatus.Pending ? null : createdAt,
            status == OperationExecutionStatus.Succeeded ? createdAt : null,
            60,
            null,
            null);
}
