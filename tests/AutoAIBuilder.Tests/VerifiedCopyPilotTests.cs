using System.Security.Cryptography;
using System.Text.Json;
using AutoAIBuilder.Application.Automation;
using AutoAIBuilder.Application.Automation.Adapters;
using AutoAIBuilder.Application.Automation.Catalog;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Orchestration;
using AutoAIBuilder.Application.Automation.Pilots;
using AutoAIBuilder.Application.Automation.Validation;
using AutoAIBuilder.Infrastructure.Automation;
using AutoAIBuilder.Infrastructure.Automation.Adapters;
using AutoAIBuilder.Infrastructure.Automation.Pilots;
using AutoAIBuilder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class VerifiedCopyPilotTests
{
    private string _directory = null!;
    private string _inputPath = null!;
    private string _outputRoot = null!;
    private SqliteAutomationAuditRepository _auditRepository = null!;
    private VerifiedCopyPilotService _pilot = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _inputPath = Path.Combine(_directory, "planta-base.dwg");
        _outputRoot = Path.Combine(_directory, "outputs");
        File.WriteAllBytes(
            _inputPath,
            "conteúdo técnico imutável"u8.ToArray());

        var database = new SqliteDatabase(
            Path.Combine(_directory, "data", "pilot.db"));
        _auditRepository = new SqliteAutomationAuditRepository(database);
        var executionService = new AutomationExecutionService(
            _auditRepository,
            [new VerifiedCopyPilotValidator()]);
        var serializer = new AutomationContractJsonSerializer();
        IAutomationAdapterRegistry adapterRegistry =
            new AutomationAdapterRegistry(
                [new VerifiedCopyAutomationAdapter(serializer)]);
        IAutomationMaskCatalogRepository maskCatalogRepository =
            new SqliteAutomationMaskCatalogRepository(database);
        IAutomationIntegrationAssessmentRepository assessmentRepository =
            new SqliteAutomationIntegrationAssessmentRepository(database);
        IAutomationOrchestrator orchestrator =
            new SafeAutomationOrchestrator(
                maskCatalogRepository,
                adapterRegistry,
                assessmentRepository,
                executionService,
                serializer,
                new AutomationContractValidator());
        _pilot = new VerifiedCopyPilotService(
            new AutomationPlanService(new AutomationContractValidator()),
            orchestrator,
            _auditRepository);
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
    public async Task FullFlow_SimulatesThenPublishesVerifiedCopyAndManifest()
    {
        var originalBytes = File.ReadAllBytes(_inputPath);

        var simulation = await _pilot.SimulateAsync(CreateRequest());

        Assert.AreEqual(
            AutomationAuditStatus.Simulated,
            simulation.Outcome.Status);
        Assert.IsTrue(simulation.Plan.IsValid);
        Assert.IsFalse(Directory.Exists(_outputRoot));
        Assert.AreEqual(1, simulation.Plan.Inputs.Count);
        Assert.AreEqual(
            ComputeSha256(originalBytes),
            simulation.Plan.Inputs.Single().Sha256);

        var outcome = await _pilot.ExecuteAsync(simulation.Plan);

        Assert.AreEqual(AutomationAuditStatus.Succeeded, outcome.Status);
        Assert.IsNotNull(outcome.PublishedPath);
        Assert.IsTrue(Directory.Exists(outcome.PublishedPath));
        var copiedFile = Path.Combine(
            outcome.PublishedPath,
            "result",
            VerifiedCopyPilotConstants.VerifiedFilesDirectory,
            Path.GetFileName(_inputPath));
        var manifestPath = Path.Combine(
            outcome.PublishedPath,
            "result",
            VerifiedCopyPilotConstants.ManifestFileName);
        Assert.IsTrue(File.Exists(copiedFile));
        Assert.IsTrue(File.Exists(manifestPath));
        CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(_inputPath));
        CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(copiedFile));

        using var manifest = JsonDocument.Parse(
            File.ReadAllText(manifestPath));
        Assert.AreEqual(
            VerifiedCopyPilotConstants.MaskId,
            manifest.RootElement.GetProperty("pilotId").GetString());
        Assert.AreEqual(
            simulation.Plan.Inputs.Single().Sha256,
            manifest.RootElement
                .GetProperty("verifiedCopySha256")
                .GetString());
        var audits = _auditRepository.GetRecent();
        Assert.AreEqual(2, audits.Count);
        Assert.IsTrue(audits.All(
            audit => audit.AdapterId
                == VerifiedCopyPilotConstants.AdapterId));
        Assert.IsTrue(audits.All(
            audit => audit.AdapterVersion
                == VerifiedCopyPilotConstants.AdapterVersion));
        Assert.IsTrue(audits.All(
            audit => audit.ContractSha256?.Length == 64));
        Assert.IsTrue(audits.All(
            audit => audit.RuleCatalogId
                == VerifiedCopyPilotConstants.CatalogId));
    }

    [TestMethod]
    public async Task Execute_ReusesResultAfterIdenticalSuccessfulRun()
    {
        var simulation = await _pilot.SimulateAsync(CreateRequest());
        var first = await _pilot.ExecuteAsync(simulation.Plan);
        var second = await _pilot.ExecuteAsync(simulation.Plan);

        Assert.AreEqual(AutomationAuditStatus.Succeeded, first.Status);
        Assert.AreEqual(AutomationAuditStatus.Reused, second.Status);
        Assert.IsTrue(second.ReusedPreviousResult);
        Assert.AreEqual(first.PublishedPath, second.PublishedPath);
    }

    [TestMethod]
    public async Task Execute_RebuildsWhenPreviouslyPublishedEvidenceIsMissing()
    {
        var simulation = await _pilot.SimulateAsync(CreateRequest());
        var first = await _pilot.ExecuteAsync(simulation.Plan);
        Assert.IsNotNull(first.PublishedPath);
        var publishedPath = first.PublishedPath;
        Directory.Delete(publishedPath, recursive: true);

        var second = await _pilot.ExecuteAsync(simulation.Plan);

        Assert.AreEqual(AutomationAuditStatus.Succeeded, second.Status);
        Assert.IsFalse(second.ReusedPreviousResult);
        Assert.AreNotEqual(publishedPath, second.PublishedPath);
        Assert.IsTrue(Directory.Exists(second.PublishedPath));
    }

    [TestMethod]
    public async Task Simulation_ProjectBlockingIssueRejectsWithoutCreatingOutput()
    {
        var request = CreateRequest() with
        {
            ProjectValidationIssues =
            [
                AutomationValidationIssue.Error(
                    "projeto",
                    "PROJECT_DATA",
                    "O projeto não está pronto.")
            ]
        };

        var simulation = await _pilot.SimulateAsync(request);

        Assert.AreEqual(
            AutomationAuditStatus.Rejected,
            simulation.Outcome.Status);
        Assert.IsFalse(simulation.Plan.IsValid);
        Assert.IsFalse(Directory.Exists(_outputRoot));
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _pilot.ExecuteAsync(simulation.Plan));
    }

    [TestMethod]
    public async Task Execute_RequiresTheAuditedSimulationPlan()
    {
        var simulation = await _pilot.SimulateAsync(CreateRequest());
        var forgedPlan = simulation.Plan with { Id = Guid.NewGuid() };

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _pilot.ExecuteAsync(forgedPlan));
        Assert.IsFalse(Directory.Exists(_outputRoot));
    }

    [TestMethod]
    public async Task Execute_BlocksWhenOriginalChangesAfterSimulation()
    {
        var simulation = await _pilot.SimulateAsync(CreateRequest());
        File.AppendAllText(_inputPath, "alteração posterior");

        var outcome = await _pilot.ExecuteAsync(simulation.Plan);

        Assert.AreEqual(
            AutomationAuditStatus.FailedRolledBack,
            outcome.Status);
        Assert.IsNull(outcome.PublishedPath);
        Assert.IsFalse(Directory.Exists(_outputRoot));
        Assert.IsTrue(outcome.ValidationIssues.Any(
            issue => issue.Message.Contains(
                "checksum",
                StringComparison.OrdinalIgnoreCase)));
    }

    private VerifiedCopyPilotRequest CreateRequest() =>
        new(
            Guid.NewGuid(),
            _inputPath,
            _outputRoot,
            []);

    private static string ComputeSha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));
}
