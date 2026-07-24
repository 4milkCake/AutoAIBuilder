using AutoAIBuilder.Application.History;
using AutoAIBuilder.Infrastructure.Persistence;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class JsonActivityLogRepositoryTests
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
    public void Append_RoundTripsEntry()
    {
        var repository = CreateRepository();
        var service = new ActivityLogService(repository);
        var projectId = Guid.NewGuid();

        service.Record(
            "Projetos",
            "Projeto criado",
            "Projeto de teste criado.",
            ActivityLevel.Success,
            projectId,
            "Projeto teste",
            new DateTimeOffset(2026, 7, 24, 14, 0, 0, TimeSpan.FromHours(-3)));

        var loaded = service.GetRecent(projectId);

        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual("Projeto criado", loaded[0].Action);
        Assert.AreEqual(ActivityLevel.Success, loaded[0].Level);
    }

    [TestMethod]
    public void GetAll_CorruptedFileReturnsEmptyWithoutOverwritingSource()
    {
        var path = Path.Combine(_testDirectory, "activity-log.json");
        File.WriteAllText(path, "{ histórico inválido");
        var repository = new JsonActivityLogRepository(path);

        var loaded = repository.GetAll();

        Assert.AreEqual(0, loaded.Count);
        Assert.AreEqual("{ histórico inválido", File.ReadAllText(path));
    }

    [TestMethod]
    public void Append_CorruptedFileIsPreserved()
    {
        var path = Path.Combine(_testDirectory, "activity-log.json");
        File.WriteAllText(path, "{ histórico inválido");
        var repository = new JsonActivityLogRepository(path);

        Assert.ThrowsException<InvalidDataException>(() =>
            repository.Append(new ActivityLogEntry(
                Guid.NewGuid(),
                DateTimeOffset.Now,
                "Sistema",
                "Teste",
                "Evento que não deve sobrescrever o arquivo.",
                ActivityLevel.Information)));
        Assert.AreEqual("{ histórico inválido", File.ReadAllText(path));
    }

    [TestMethod]
    public void Append_KeepsOnlyConfiguredMaximumEntries()
    {
        var repository = CreateRepository();
        var start = new DateTimeOffset(2026, 7, 24, 14, 0, 0, TimeSpan.FromHours(-3));

        for (var index = 0; index < JsonActivityLogRepository.MaximumEntries + 3; index++)
        {
            repository.Append(new ActivityLogEntry(
                Guid.NewGuid(),
                start.AddSeconds(index),
                "Sistema",
                $"Evento {index}",
                "Teste de retenção.",
                ActivityLevel.Information));
        }

        var loaded = repository.GetAll();

        Assert.AreEqual(JsonActivityLogRepository.MaximumEntries, loaded.Count);
        Assert.AreEqual("Evento 3", loaded[0].Action);
    }

    private JsonActivityLogRepository CreateRepository() =>
        new(Path.Combine(_testDirectory, "activity-log.json"));
}
