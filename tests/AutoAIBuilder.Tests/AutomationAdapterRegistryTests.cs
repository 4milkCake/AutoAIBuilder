using AutoAIBuilder.Application.Automation;
using AutoAIBuilder.Application.Automation.Adapters;
using AutoAIBuilder.Application.Automation.Catalog;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Orchestration;
using AutoAIBuilder.Application.Automation.Validation;
using AutoAIBuilder.Infrastructure.Automation;
using AutoAIBuilder.Infrastructure.Automation.Adapters;
using AutoAIBuilder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class AutomationAdapterRegistryTests
{
    private string _directory = null!;
    private SqliteDatabase _database = null!;
    private AutomationContractJsonSerializer _serializer = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _database = new SqliteDatabase(
            Path.Combine(_directory, "data", "registry.db"));
        _serializer = new AutomationContractJsonSerializer();
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
    public void Registry_RequiresExactIdentityVersionAndFingerprint()
    {
        var adapter = CreateAdapter(catalogExecutionEnabled: false);
        var registry = new AutomationAdapterRegistry([adapter]);

        var exact = registry.Resolve(
            adapter.Descriptor.MaskId,
            adapter.Descriptor.MaskVersion,
            adapter.Descriptor.ContractSha256);
        var mismatch = registry.Resolve(
            adapter.Descriptor.MaskId,
            adapter.Descriptor.MaskVersion,
            new string('A', 64));
        var missing = registry.Resolve(
            "outra-mascara",
            "1.0.0",
            adapter.Descriptor.ContractSha256);

        Assert.IsTrue(exact.IsResolved);
        Assert.AreSame(adapter, exact.Adapter);
        Assert.AreEqual(
            AutomationAdapterResolutionStatus.ContractMismatch,
            mismatch.Status);
        Assert.IsNull(mismatch.Adapter);
        Assert.AreEqual(
            AutomationAdapterResolutionStatus.NotRegistered,
            missing.Status);
        Assert.ThrowsException<InvalidOperationException>(
            () => new AutomationAdapterRegistry([adapter, adapter]));
    }

    [TestMethod]
    public void Assessment_RevalidatesAndAuditsWithoutInvokingAdapter()
    {
        var repository =
            new SqliteAutomationMaskCatalogRepository(_database);
        var entry = AddCatalogEntry(repository, isActive: true);
        var adapter = CreateAdapter(catalogExecutionEnabled: false);
        var assessments =
            new SqliteAutomationIntegrationAssessmentRepository(_database);
        var orchestrator = CreateOrchestrator(
            repository,
            assessments,
            adapter);

        var assessment = orchestrator.AssessIntegration(
            Guid.NewGuid(),
            entry.Id);

        Assert.AreEqual(
            AutomationIntegrationStatus.CatalogExecutionBlocked,
            assessment.Status);
        Assert.AreEqual(
            adapter.Descriptor.AdapterId,
            assessment.AdapterId);
        Assert.AreEqual(0, adapter.ExecutionCount);
        Assert.AreEqual(1, assessments.GetRecent().Count);
    }

    [TestMethod]
    public async Task GenericCatalogExecution_AlwaysStopsAfterAuditedAssessment()
    {
        var repository =
            new SqliteAutomationMaskCatalogRepository(_database);
        var entry = AddCatalogEntry(repository, isActive: true);
        var adapter = CreateAdapter(catalogExecutionEnabled: true);
        var assessments =
            new SqliteAutomationIntegrationAssessmentRepository(_database);
        var orchestrator = CreateOrchestrator(
            repository,
            assessments,
            adapter);

        var result = await orchestrator.ExecuteAsync(
            AutomationRequest.Create(
                Guid.NewGuid(),
                entry.MaskId));

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(0, adapter.ExecutionCount);
        Assert.AreEqual(
            AutomationIntegrationStatus.Ready,
            assessments.GetRecent().Single().Status);
        StringAssert.Contains(result.Summary, "bloqueada");
    }

    [TestMethod]
    public void Assessment_BlocksTamperedStoredSnapshot()
    {
        var repository =
            new SqliteAutomationMaskCatalogRepository(_database);
        var entry = AddCatalogEntry(repository, isActive: true);
        using (var connection = _database.OpenConnection())
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE AutomationMaskCatalog
                SET MaskJson = '{}'
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$id", entry.Id.ToString("D"));
            command.ExecuteNonQuery();
        }

        var adapter = CreateAdapter(catalogExecutionEnabled: true);
        var assessments =
            new SqliteAutomationIntegrationAssessmentRepository(_database);
        var orchestrator = CreateOrchestrator(
            repository,
            assessments,
            adapter);

        var assessment = orchestrator.AssessIntegration(
            Guid.NewGuid(),
            entry.Id);

        Assert.AreEqual(
            AutomationIntegrationStatus.InvalidCatalogContract,
            assessment.Status);
        Assert.AreEqual(0, adapter.ExecutionCount);
        Assert.AreEqual(1, assessments.GetRecent().Count);
    }

    [TestMethod]
    public async Task ExecutePlan_UsesExactRegistryAndPersistsAdapterEvidence()
    {
        var inputPath = Path.Combine(_directory, "entrada.txt");
        File.WriteAllText(inputPath, "conteúdo imutável");
        var plan = await new AutomationPlanService(
                new AutomationContractValidator())
            .CreatePlanAsync(
                new AutomationPlanRequest(
                    Guid.NewGuid(),
                    AutomationTestData.CreateCatalog(),
                    AutomationTestData.CreateMask(),
                    [inputPath],
                    Path.Combine(_directory, "output"),
                    new Dictionary<string, string>
                    {
                        ["modo"] = "seguro"
                    }),
                AutomationExecutionMode.Simulation);
        var adapter = CreateAdapter(
            catalogExecutionEnabled: false,
            contractSha256: plan.ContractSha256);
        var auditRepository =
            new SqliteAutomationAuditRepository(_database);
        var orchestrator = new SafeAutomationOrchestrator(
            new SqliteAutomationMaskCatalogRepository(_database),
            new AutomationAdapterRegistry([adapter]),
            new SqliteAutomationIntegrationAssessmentRepository(_database),
            new AutomationExecutionService(auditRepository),
            _serializer,
            new AutomationContractValidator());

        var outcome = await orchestrator.ExecutePlanAsync(plan);

        Assert.AreEqual(
            AutomationAuditStatus.Simulated,
            outcome.Status);
        Assert.AreEqual(0, adapter.ExecutionCount);
        var audit = auditRepository.Get(outcome.AuditId);
        Assert.IsNotNull(audit);
        Assert.AreEqual(
            adapter.Descriptor.AdapterId,
            audit.AdapterId);
        Assert.AreEqual(
            plan.ContractSha256,
            audit.ContractSha256);
        Assert.AreNotEqual(
            plan.IdempotencyKey,
            audit.IdempotencyKey);
        Assert.AreEqual(
            AutomationTestData.CreateCatalog().CatalogId,
            audit.RuleCatalogId);

        var forged = plan with { ContractSha256 = new string('F', 64) };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => orchestrator.ExecutePlanAsync(forged));
        var forgedMask = plan with
        {
            Mask = plan.Mask with
            {
                Description = "Conteúdo adulterado após o planejamento."
            }
        };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => orchestrator.ExecutePlanAsync(forgedMask));
        Assert.AreEqual(0, adapter.ExecutionCount);
    }

    private SafeAutomationOrchestrator CreateOrchestrator(
        IAutomationMaskCatalogRepository repository,
        IAutomationIntegrationAssessmentRepository assessments,
        FakeAdapter adapter)
    {
        var auditRepository =
            new SqliteAutomationAuditRepository(_database);
        return new SafeAutomationOrchestrator(
            repository,
            new AutomationAdapterRegistry([adapter]),
            assessments,
            new AutomationExecutionService(auditRepository),
            _serializer,
            new AutomationContractValidator());
    }

    private AutomationMaskCatalogEntry AddCatalogEntry(
        IAutomationMaskCatalogRepository repository,
        bool isActive)
    {
        var mask = AutomationTestData.CreateMask();
        var catalog = AutomationTestData.CreateCatalog();
        var maskJson = _serializer.Serialize(mask);
        var catalogJson = _serializer.Serialize(catalog);
        var now = DateTimeOffset.UtcNow;
        var entry = new AutomationMaskCatalogEntry(
            Guid.NewGuid(),
            mask.Id,
            mask.Version,
            mask.Name,
            mask.Discipline,
            mask.Description,
            mask.MinimumApplicationVersion,
            catalog.CatalogId,
            catalog.Version,
            catalog.Rules.Count,
            mask.Dependencies.Count,
            mask.Parameters.Count,
            mask.Outputs.Count,
            mask.SupportsSimulation,
            mask.IsIdempotent,
            maskJson,
            catalogJson,
            AutomationMaskPackageFingerprint.Compute(maskJson, catalogJson),
            "mask.json",
            "rules.json",
            isActive,
            now,
            now);
        repository.Add(entry);
        return entry;
    }

    private FakeAdapter CreateAdapter(
        bool catalogExecutionEnabled,
        string? contractSha256 = null)
    {
        var mask = AutomationTestData.CreateMask();
        var catalog = AutomationTestData.CreateCatalog();
        var fingerprint = contractSha256
            ?? AutomationMaskPackageFingerprint.Compute(
                _serializer.Serialize(mask),
                _serializer.Serialize(catalog));
        return new FakeAdapter(
            new AutomationAdapterDescriptor(
                "autoaibuilder.teste-seguro",
                "1.0.0",
                "Adaptador seguro de teste",
                "Adaptador em memória usado somente pelos testes.",
                "AutoAIBuilder.Tests",
                mask.Id,
                mask.Version,
                fingerprint,
                true,
                true,
                true,
                catalogExecutionEnabled,
                AutomationAdapterOrigin.BuiltIn));
    }

    private sealed class FakeAdapter(
        AutomationAdapterDescriptor descriptor) : IAutomationAdapter
    {
        public int ExecutionCount { get; private set; }

        public AutomationAdapterDescriptor Descriptor { get; } = descriptor;

        public Task<AutomationAdapterResult> ExecuteAsync(
            AutomationWorkspaceContext context,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult(
                AutomationAdapterResult.Failure(
                    "O adaptador de teste não deve produzir saídas."));
        }
    }
}
