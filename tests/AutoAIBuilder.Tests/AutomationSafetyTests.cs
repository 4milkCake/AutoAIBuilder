using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Validation;
using AutoAIBuilder.Infrastructure.Automation;
using AutoAIBuilder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class AutomationSafetyTests
{
    private string _testDirectory = null!;
    private string _inputPath = null!;
    private string _outputRoot = null!;
    private SqliteDatabase _database = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
        _inputPath = Path.Combine(_testDirectory, "original.txt");
        _outputRoot = Path.Combine(_testDirectory, "output");
        File.WriteAllText(_inputPath, "conteúdo original");
        _database = new SqliteDatabase(
            Path.Combine(_testDirectory, "data", "automation.db"));
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
    public async Task Simulation_ProducesAuditedPlanWithoutCreatingOutput()
    {
        var plan = await CreatePlanAsync(AutomationExecutionMode.Simulation);
        var adapterWasCalled = false;
        var service = new AutomationExecutionService(
            new SqliteAutomationAuditRepository(_database));

        var outcome = await service.ExecuteAsync(
            plan,
            (_, _) =>
            {
                adapterWasCalled = true;
                return Task.FromResult(
                    AutomationAdapterResult.Success("Não deveria executar."));
            });

        Assert.IsTrue(plan.IsValid);
        Assert.AreEqual(AutomationAuditStatus.Simulated, outcome.Status);
        Assert.IsFalse(adapterWasCalled);
        Assert.IsFalse(Directory.Exists(_outputRoot));
        Assert.AreEqual(
            AutomationAuditStatus.Simulated,
            new SqliteAutomationAuditRepository(_database)
                .Get(outcome.AuditId)
                ?.Status);
    }

    [TestMethod]
    public async Task Apply_WorksOnlyOnCopyAndReusesSuccessfulResult()
    {
        var plan = await CreatePlanAsync(AutomationExecutionMode.Apply);
        var repository = new SqliteAutomationAuditRepository(_database);
        var service = new AutomationExecutionService(repository);
        var adapterCalls = 0;

        async Task<AutomationAdapterResult> Adapter(
            AutomationWorkspaceContext context,
            CancellationToken cancellationToken)
        {
            adapterCalls++;
            Assert.AreNotEqual(_inputPath, context.InputCopies.Single());
            CollectionAssert.AreEqual(
                File.ReadAllBytes(_inputPath),
                File.ReadAllBytes(context.InputCopies.Single()));
            var resultPath = Path.Combine(
                context.ResultDirectory,
                "processed.txt");
            await File.WriteAllTextAsync(
                resultPath,
                "resultado criado sobre a cópia",
                cancellationToken);
            return AutomationAdapterResult.Success(
                "Saída produzida e validada.",
                [resultPath]);
        }

        var first = await service.ExecuteAsync(plan, Adapter);
        var second = await service.ExecuteAsync(plan, Adapter);

        Assert.AreEqual("conteúdo original", File.ReadAllText(_inputPath));
        Assert.AreEqual(AutomationAuditStatus.Succeeded, first.Status);
        Assert.IsNotNull(first.PublishedPath);
        Assert.IsTrue(Directory.Exists(first.PublishedPath));
        Assert.IsTrue(File.Exists(first.OutputPaths.Single()));
        Assert.AreEqual(AutomationAuditStatus.Reused, second.Status);
        Assert.IsTrue(second.ReusedPreviousResult);
        Assert.AreEqual(first.PublishedPath, second.PublishedPath);
        Assert.AreEqual(1, adapterCalls);
        Assert.AreEqual(2, repository.GetRecent().Count);
    }

    [TestMethod]
    public async Task Apply_PreservesPartialArtifactsWhenAdapterFails()
    {
        var plan = await CreatePlanAsync(AutomationExecutionMode.Apply);
        var service = new AutomationExecutionService(
            new SqliteAutomationAuditRepository(_database));

        var outcome = await service.ExecuteAsync(
            plan,
            async (context, cancellationToken) =>
            {
                await File.WriteAllTextAsync(
                    Path.Combine(context.ResultDirectory, "partial.txt"),
                    "artefato recuperável",
                    cancellationToken);
                throw new InvalidOperationException("Falha controlada de teste.");
            });

        Assert.AreEqual(
            AutomationAuditStatus.FailedRolledBack,
            outcome.Status);
        Assert.IsNotNull(outcome.RecoveryPath);
        Assert.IsTrue(Directory.Exists(outcome.RecoveryPath));
        Assert.IsTrue(File.Exists(
            Path.Combine(outcome.RecoveryPath, "result", "partial.txt")));
        Assert.AreEqual("conteúdo original", File.ReadAllText(_inputPath));
    }

    [TestMethod]
    public async Task Apply_BlocksChangedOriginalBeforeCreatingWorkspace()
    {
        var plan = await CreatePlanAsync(AutomationExecutionMode.Apply);
        File.WriteAllText(_inputPath, "alterado depois da simulação");
        var service = new AutomationExecutionService(
            new SqliteAutomationAuditRepository(_database));
        var adapterWasCalled = false;

        var outcome = await service.ExecuteAsync(
            plan,
            (_, _) =>
            {
                adapterWasCalled = true;
                return Task.FromResult(
                    AutomationAdapterResult.Success("Inválido."));
            });

        Assert.AreEqual(
            AutomationAuditStatus.FailedRolledBack,
            outcome.Status);
        Assert.IsFalse(adapterWasCalled);
        Assert.IsNull(outcome.RecoveryPath);
        Assert.IsFalse(Directory.Exists(_outputRoot));
        Assert.IsTrue(outcome.ValidationIssues.Any(
            issue => issue.Code == "execution.failed"
                     && issue.Message.Contains(
                         "checksum",
                         StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task Apply_QuarantinesOutputRejectedByPostValidator()
    {
        var plan = await CreatePlanAsync(AutomationExecutionMode.Apply);
        var service = new AutomationExecutionService(
            new SqliteAutomationAuditRepository(_database),
            [new RejectingPostValidator()]);

        var outcome = await service.ExecuteAsync(
            plan,
            async (context, cancellationToken) =>
            {
                var resultPath = Path.Combine(
                    context.ResultDirectory,
                    "processed.txt");
                await File.WriteAllTextAsync(
                    resultPath,
                    "saída",
                    cancellationToken);
                return AutomationAdapterResult.Success(
                    "Criada.",
                    [resultPath]);
            });

        Assert.AreEqual(
            AutomationAuditStatus.FailedRolledBack,
            outcome.Status);
        Assert.IsNotNull(outcome.RecoveryPath);
        Assert.IsTrue(outcome.ValidationIssues.Any(
            issue => issue.Code == "test.post-validation"));
        Assert.AreEqual("conteúdo original", File.ReadAllText(_inputPath));
    }

    [TestMethod]
    public async Task Planning_RejectsIncompatibleApplicationVersion()
    {
        var mask = AutomationTestData.CreateMask() with
        {
            MinimumApplicationVersion = "99.0.0"
        };
        var plan = await new AutomationPlanService(
                new AutomationContractValidator())
            .CreatePlanAsync(
                new AutomationPlanRequest(
                    Guid.NewGuid(),
                    AutomationTestData.CreateCatalog(),
                    mask,
                    [_inputPath],
                    _outputRoot,
                    new Dictionary<string, string>
                    {
                        ["modo"] = "seguro"
                    }),
                AutomationExecutionMode.Simulation);

        Assert.IsFalse(plan.IsValid);
        Assert.IsTrue(plan.ValidationIssues.Any(
            issue => issue.Code == "plan.application-version"));
        Assert.IsFalse(Directory.Exists(_outputRoot));
    }

    [TestMethod]
    public async Task Apply_DoesNotReuseMaskThatDeclaresNonIdempotentBehavior()
    {
        var mask = AutomationTestData.CreateMask() with
        {
            IsIdempotent = false
        };
        var planner = new AutomationPlanService(
            new AutomationContractValidator());
        var plan = await planner.CreatePlanAsync(
            new AutomationPlanRequest(
                Guid.NewGuid(),
                AutomationTestData.CreateCatalog(),
                mask,
                [_inputPath],
                _outputRoot,
                new Dictionary<string, string>
                {
                    ["modo"] = "seguro"
                }),
            AutomationExecutionMode.Apply);
        var service = new AutomationExecutionService(
            new SqliteAutomationAuditRepository(_database));
        var calls = 0;

        Task<AutomationAdapterResult> Adapter(
            AutomationWorkspaceContext context,
            CancellationToken cancellationToken)
        {
            calls++;
            var resultPath = Path.Combine(
                context.ResultDirectory,
                "processed.txt");
            File.WriteAllText(resultPath, $"execução {calls}");
            return Task.FromResult(
                AutomationAdapterResult.Success("Concluída.", [resultPath]));
        }

        var first = await service.ExecuteAsync(plan, Adapter);
        var second = await service.ExecuteAsync(plan, Adapter);

        Assert.AreEqual(AutomationAuditStatus.Succeeded, first.Status);
        Assert.AreEqual(AutomationAuditStatus.Succeeded, second.Status);
        Assert.AreEqual(2, calls);
        Assert.AreNotEqual(first.PublishedPath, second.PublishedPath);
    }

    private Task<AutomationExecutionPlan> CreatePlanAsync(
        AutomationExecutionMode mode) =>
        new AutomationPlanService(new AutomationContractValidator())
            .CreatePlanAsync(
                new AutomationPlanRequest(
                    Guid.NewGuid(),
                    AutomationTestData.CreateCatalog(),
                    AutomationTestData.CreateMask(),
                    [_inputPath],
                    _outputRoot,
                    new Dictionary<string, string>
                    {
                        ["modo"] = "seguro"
                    }),
                mode);

    private sealed class RejectingPostValidator :
        IAutomationExecutionValidator
    {
        public Task<IReadOnlyList<AutomationValidationIssue>>
            ValidateBeforeAsync(
                AutomationExecutionPlan plan,
                CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AutomationValidationIssue>>([]);

        public Task<IReadOnlyList<AutomationValidationIssue>>
            ValidateAfterAsync(
                AutomationExecutionPlan plan,
                AutomationWorkspaceContext workspace,
                AutomationAdapterResult adapterResult,
                CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AutomationValidationIssue>>(
                [
                    AutomationValidationIssue.Error(
                        "pós-validação",
                        "test.post-validation",
                        "Saída rejeitada pelo validador de teste.")
                ]);
    }
}
