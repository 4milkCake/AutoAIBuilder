using AutoAIBuilder.Application.Automation.Catalog;
using AutoAIBuilder.Application.Automation.Validation;
using AutoAIBuilder.Infrastructure.Automation;
using AutoAIBuilder.Infrastructure.Automation.Catalog;
using AutoAIBuilder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class AutomationMaskCatalogTests
{
    private string _directory = null!;
    private SqliteAutomationMaskCatalogRepository _repository = null!;
    private AutomationMaskCatalogService _service = null!;
    private AutomationContractJsonSerializer _serializer = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        var database = new SqliteDatabase(
            Path.Combine(_directory, "data", "catalog.db"));
        _repository = new SqliteAutomationMaskCatalogRepository(database);
        _serializer = new AutomationContractJsonSerializer();
        _service = new AutomationMaskCatalogService(
            _repository,
            _serializer,
            new AutomationContractValidator());
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task AnalyzeAndImport_StoresNormalizedInactivePackage()
    {
        var files = WritePackage(
            AutomationTestData.CreateMask(),
            AutomationTestData.CreateCatalog());
        var originalMask = File.ReadAllText(files.MaskPath);
        var originalCatalog = File.ReadAllText(files.CatalogPath);

        var preview = await _service.AnalyzeAsync(
            files.MaskPath,
            files.CatalogPath);
        var result = _service.Import(preview);

        Assert.IsTrue(preview.IsValid);
        Assert.IsTrue(preview.CanImport);
        Assert.AreEqual(64, preview.ContentSha256.Length);
        Assert.AreEqual(
            AutomationMaskImportStatus.Imported,
            result.Status);
        Assert.IsNotNull(result.Entry);
        Assert.IsFalse(result.Entry.IsActive);
        Assert.AreEqual("mask.json", result.Entry.MaskSourceFileName);
        Assert.AreEqual(
            "rules.json",
            result.Entry.RuleCatalogSourceFileName);
        Assert.AreEqual(1, result.Entry.RuleCount);
        Assert.AreEqual(1, _repository.GetAll().Count);
        Assert.AreEqual(originalMask, File.ReadAllText(files.MaskPath));
        Assert.AreEqual(originalCatalog, File.ReadAllText(files.CatalogPath));

        var duplicate = await _service.AnalyzeAsync(
            files.MaskPath,
            files.CatalogPath);
        Assert.AreEqual(
            AutomationMaskCatalogConflict.AlreadyImported,
            duplicate.Conflict);
        Assert.IsFalse(duplicate.CanImport);
        Assert.AreEqual(
            AutomationMaskImportStatus.AlreadyImported,
            _service.Import(duplicate).Status);
        Assert.AreEqual(1, _repository.GetAll().Count);
    }

    [TestMethod]
    public async Task Analyze_BlocksSameVersionWithDifferentContent()
    {
        var firstFiles = WritePackage(
            AutomationTestData.CreateMask(),
            AutomationTestData.CreateCatalog(),
            "first");
        var firstPreview = await _service.AnalyzeAsync(
            firstFiles.MaskPath,
            firstFiles.CatalogPath);
        var imported = _service.Import(firstPreview);
        Assert.IsTrue(imported.Succeeded);

        var changedMask = AutomationTestData.CreateMask() with
        {
            Description = "Conteúdo diferente sem alterar a versão."
        };
        var conflictingFiles = WritePackage(
            changedMask,
            AutomationTestData.CreateCatalog(),
            "conflict");
        var conflict = await _service.AnalyzeAsync(
            conflictingFiles.MaskPath,
            conflictingFiles.CatalogPath);

        Assert.AreEqual(
            AutomationMaskCatalogConflict.ContentConflict,
            conflict.Conflict);
        Assert.IsFalse(conflict.IsValid);
        Assert.IsFalse(conflict.CanImport);
        Assert.IsTrue(conflict.Issues.Any(
            issue => issue.Code
                == "catalog.version-content-conflict"));
        Assert.AreEqual(
            AutomationMaskImportStatus.ContentConflict,
            _service.Import(conflict).Status);
        Assert.AreEqual(1, _repository.GetAll().Count);
        Assert.AreEqual(
            imported.Entry?.ContentSha256,
            _repository.GetAll().Single().ContentSha256);
    }

    [TestMethod]
    public async Task Activation_KeepsOnlyOneActiveVersionPerMask()
    {
        var versionOne = await ImportAsync(
            AutomationTestData.CreateMask() with { Version = "1.0.0" },
            "v1");
        var versionTwo = await ImportAsync(
            AutomationTestData.CreateMask() with { Version = "2.0.0" },
            "v2");

        _service.SetActive(versionOne.Id, true);
        Assert.IsTrue(_repository.Get(versionOne.Id)?.IsActive);

        _service.SetActive(versionTwo.Id, true);

        Assert.IsFalse(_repository.Get(versionOne.Id)?.IsActive);
        Assert.IsTrue(_repository.Get(versionTwo.Id)?.IsActive);
        _service.SetActive(versionTwo.Id, false);
        Assert.AreEqual(0, _repository.GetAll().Count(entry => entry.IsActive));
        Assert.AreEqual(2, _repository.GetAll().Count);
    }

    [TestMethod]
    public async Task Analyze_RejectsIncompatibleAndOversizedContracts()
    {
        var incompatibleFiles = WritePackage(
            AutomationTestData.CreateMask() with
            {
                MinimumApplicationVersion = "99.0.0"
            },
            AutomationTestData.CreateCatalog(),
            "incompatible");

        var incompatible = await _service.AnalyzeAsync(
            incompatibleFiles.MaskPath,
            incompatibleFiles.CatalogPath);

        Assert.IsFalse(incompatible.IsValid);
        Assert.IsTrue(incompatible.Issues.Any(
            issue => issue.Code
                == "catalog.incompatible-application-version"));

        var oversizedPath = Path.Combine(_directory, "oversized.json");
        File.WriteAllText(
            oversizedPath,
            new string(
                'x',
                checked((int)
                    AutomationMaskCatalogService
                        .MaximumContractFileSizeBytes + 1)));
        var oversized = await _service.AnalyzeAsync(
            oversizedPath,
            incompatibleFiles.CatalogPath);

        Assert.IsFalse(oversized.IsValid);
        Assert.IsTrue(oversized.Issues.Any(
            issue => issue.Code == "catalog.invalid-file-size"));
        Assert.AreEqual(0, _repository.GetAll().Count);
    }

    [TestMethod]
    public async Task Analyze_RejectsUnsafeContractDetails()
    {
        var invalidMask = AutomationTestData.CreateMask() with
        {
            AcceptedExtensions = [".tar.gz"],
            Parameters =
            [
                AutomationTestData.CreateMask().Parameters.Single() with
                {
                    Type = "script"
                }
            ],
            Outputs =
            [
                AutomationTestData.CreateMask().Outputs.Single(),
                AutomationTestData.CreateMask().Outputs.Single() with
                {
                    Id = "resultado-duplicado"
                }
            ]
        };
        var files = WritePackage(
            invalidMask,
            AutomationTestData.CreateCatalog(),
            "unsafe");

        var preview = await _service.AnalyzeAsync(
            files.MaskPath,
            files.CatalogPath);

        Assert.IsFalse(preview.IsValid);
        CollectionAssert.IsSubsetOf(
            new[]
            {
                "contract.invalid-extension",
                "mask.unsupported-parameter-type",
                "mask.duplicate-output-path"
            },
            preview.Issues.Select(issue => issue.Code).ToArray());
        Assert.AreEqual(0, _repository.GetAll().Count);
    }

    private async Task<AutomationMaskCatalogEntry> ImportAsync(
        AutoAIBuilder.Application.Automation.Contracts
            .AutomationMaskDefinition mask,
        string subdirectory)
    {
        var files = WritePackage(
            mask,
            AutomationTestData.CreateCatalog(),
            subdirectory);
        var preview = await _service.AnalyzeAsync(
            files.MaskPath,
            files.CatalogPath);
        var result = _service.Import(preview);
        Assert.IsTrue(result.Succeeded);
        return result.Entry!;
    }

    private (string MaskPath, string CatalogPath) WritePackage(
        AutoAIBuilder.Application.Automation.Contracts
            .AutomationMaskDefinition mask,
        AutoAIBuilder.Application.Automation.Contracts
            .AutomationRuleCatalog catalog,
        string? subdirectory = null)
    {
        var directory = subdirectory is null
            ? _directory
            : Path.Combine(_directory, subdirectory);
        Directory.CreateDirectory(directory);
        var maskPath = Path.Combine(directory, "mask.json");
        var catalogPath = Path.Combine(directory, "rules.json");
        File.WriteAllText(maskPath, _serializer.Serialize(mask));
        File.WriteAllText(catalogPath, _serializer.Serialize(catalog));
        return (maskPath, catalogPath);
    }
}
