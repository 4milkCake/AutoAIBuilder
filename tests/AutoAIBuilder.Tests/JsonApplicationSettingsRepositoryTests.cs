using AutoAIBuilder.Application.Settings;
using AutoAIBuilder.Infrastructure.Persistence;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class JsonApplicationSettingsRepositoryTests
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
    public void Load_ReturnsDefaultsWhenFileDoesNotExist()
    {
        var repository = CreateRepository();

        var settings = repository.Load();

        Assert.AreEqual("Residencial", settings.DefaultProjectType);
        Assert.AreEqual(1, settings.DefaultFloors);
        Assert.IsTrue(settings.ConfirmFileReferenceRemoval);
    }

    [TestMethod]
    public void Save_RoundTripsValidatedSettings()
    {
        var repository = CreateRepository();
        var service = new ApplicationSettingsService(repository);

        service.Save(new ApplicationSettings
        {
            DefaultProjectType = " Comercial ",
            DefaultFloors = 3,
            DefaultUnits = 12,
            ConfirmFileReferenceRemoval = false
        });

        var loaded = service.Load();

        Assert.AreEqual("Comercial", loaded.DefaultProjectType);
        Assert.AreEqual(3, loaded.DefaultFloors);
        Assert.AreEqual(12, loaded.DefaultUnits);
        Assert.IsFalse(loaded.ConfirmFileReferenceRemoval);
    }

    [TestMethod]
    public void ServiceLoad_UsesDefaultsWhenStoredJsonIsCorrupted()
    {
        var path = Path.Combine(_testDirectory, "data", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ configuração inválida");

        var service = new ApplicationSettingsService(
            new JsonApplicationSettingsRepository(path));

        var loaded = service.Load();

        Assert.AreEqual(ApplicationSettings.CreateDefault(), loaded);
        Assert.AreEqual("{ configuração inválida", File.ReadAllText(path));
    }

    private JsonApplicationSettingsRepository CreateRepository() =>
        new(Path.Combine(_testDirectory, "data", "settings.json"));
}
