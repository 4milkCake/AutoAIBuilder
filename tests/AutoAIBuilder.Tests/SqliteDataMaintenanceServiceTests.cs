using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Domain.Projects;
using AutoAIBuilder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class SqliteDataMaintenanceServiceTests
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
    public void BackupAndRestore_RoundTripStateAndCreateSafetyBackup()
    {
        var database = new SqliteDatabase(
            Path.Combine(_testDirectory, "data", "autoaibuilder.db"));
        var repository = new SqliteProjectRepository(database);
        var firstProject = ProjectWorkspace.Create(
            "Estado do backup",
            "Residencial",
            2,
            8);
        repository.Save(firstProject);
        new SqliteActiveProjectStateRepository(database).Save(firstProject.Id);

        var service = new SqliteDataMaintenanceService(
            database,
            _ => { });
        var backupPath = Path.Combine(
            _testDirectory,
            "exports",
            "estado.aabbackup");
        var backup = service.CreateBackup(backupPath);

        var laterProject = ProjectWorkspace.Create(
            "Estado posterior",
            "Comercial",
            5,
            20);
        repository.Save(laterProject);
        new SqliteActiveProjectStateRepository(database).Save(laterProject.Id);

        var restore = service.RestoreBackup(backupPath);

        var projects = new SqliteProjectRepository(database).GetAll();
        Assert.AreEqual(1, projects.Count);
        Assert.AreEqual(firstProject.Id, projects.Single().Id);
        Assert.AreEqual(
            firstProject.Id,
            new SqliteActiveProjectStateRepository(database).Load());
        Assert.IsTrue(File.Exists(backup.FilePath));
        Assert.IsTrue(File.Exists(restore.SafetyBackupFilePath));
        Assert.AreEqual("ok", database.QuickCheck());
    }

    [TestMethod]
    public void Restore_InvalidFileDoesNotChangeCurrentDatabase()
    {
        var database = new SqliteDatabase(
            Path.Combine(_testDirectory, "data", "autoaibuilder.db"));
        var repository = new SqliteProjectRepository(database);
        var project = ProjectWorkspace.Create(
            "Projeto preservado",
            "Residencial",
            1,
            1);
        repository.Save(project);

        var invalidBackup = Path.Combine(_testDirectory, "invalid.aabbackup");
        File.WriteAllText(invalidBackup, "não é um banco SQLite");
        var service = new SqliteDataMaintenanceService(database, _ => { });

        Assert.ThrowsException<SqliteException>(() =>
            service.RestoreBackup(invalidBackup));
        Assert.AreEqual(
            project.Id,
            new SqliteProjectRepository(database).GetAll().Single().Id);
    }

    [TestMethod]
    public void Relocate_CopiesValidatedDatabaseAndPreservesOriginal()
    {
        var database = new SqliteDatabase(
            Path.Combine(_testDirectory, "current", "autoaibuilder.db"));
        var repository = new SqliteProjectRepository(database);
        var project = ProjectWorkspace.Create(
            "Projeto realocado",
            "Industrial",
            1,
            1);
        repository.Save(project);

        string? configuredDirectory = null;
        var service = new SqliteDataMaintenanceService(
            database,
            path => configuredDirectory = path);
        var newDirectory = Path.Combine(_testDirectory, "new-location");

        var result = service.RelocateDataDirectory(newDirectory);

        Assert.IsTrue(result.RequiresRestart);
        Assert.AreEqual(Path.GetFullPath(newDirectory), configuredDirectory);
        Assert.IsTrue(File.Exists(database.DatabasePath));
        Assert.IsTrue(File.Exists(result.NewDatabasePath));
        Assert.AreEqual(
            project.Id,
            new SqliteProjectRepository(
                    new SqliteDatabase(result.NewDatabasePath))
                .GetAll()
                .Single()
                .Id);
    }
}
