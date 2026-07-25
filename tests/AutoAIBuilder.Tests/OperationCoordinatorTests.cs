using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.Operations;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class OperationCoordinatorTests
{
    [TestMethod]
    public async Task RunAsync_PersistsProgressAndSuccessfulCompletion()
    {
        var repository = new InMemoryOperationRepository();
        var logger = new InMemoryDiagnosticLogger();
        var coordinator = new OperationCoordinator(repository, logger);
        var observedProgress = new List<OperationProgress>();
        var request = OperationRequest.Create(
            "test-success",
            "Operação de teste",
            "resource-a");

        var result = await coordinator.RunAsync(
            request,
            (context, _) =>
            {
                context.Report(15, "Preparando");
                context.Report(70, "Processando");
                return Task.CompletedTask;
            },
            new InlineProgress<OperationProgress>(observedProgress.Add));

        Assert.AreEqual(OperationExecutionStatus.Succeeded, result.Status);
        Assert.AreEqual(100, result.Progress);
        Assert.IsTrue(result.IsTerminal);
        Assert.AreEqual(result, repository.Get(request.Id));
        CollectionAssert.AreEqual(
            new[] { 15, 70, 100 },
            observedProgress.Select(item => item.Percentage).ToArray());
        Assert.IsFalse(coordinator.IsResourceBusy("resource-a"));
        Assert.IsTrue(
            logger.Entries.Any(entry =>
                entry.Message.Contains("concluída", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task RunAsync_IsolatesFailureAndReleasesResource()
    {
        var repository = new InMemoryOperationRepository();
        var coordinator = new OperationCoordinator(
            repository,
            new InMemoryDiagnosticLogger());
        var failedRequest = OperationRequest.Create(
            "test-failure",
            "Operação com falha",
            "exclusive-resource");

        var failed = await coordinator.RunAsync(
            failedRequest,
            (_, _) => throw new InvalidOperationException("falha controlada"));

        Assert.AreEqual(OperationExecutionStatus.Failed, failed.Status);
        Assert.AreEqual("InvalidOperationException", failed.ErrorCode);
        StringAssert.Contains(failed.ErrorMessage, "falha controlada");
        Assert.IsFalse(coordinator.IsResourceBusy("exclusive-resource"));

        var next = await coordinator.RunAsync(
            OperationRequest.Create(
                "test-next",
                "Operação seguinte",
                "exclusive-resource"),
            (_, _) => Task.CompletedTask);

        Assert.AreEqual(OperationExecutionStatus.Succeeded, next.Status);
    }

    [TestMethod]
    public async Task RunAsync_RejectsIncompatibleConcurrentOperation()
    {
        var coordinator = new OperationCoordinator(
            new InMemoryOperationRepository(),
            new InMemoryDiagnosticLogger());
        var started = NewSignal();
        var release = NewSignal();

        var firstTask = coordinator.RunAsync(
            OperationRequest.Create(
                "first",
                "Primeira operação",
                "shared-resource"),
            async (_, _) =>
            {
                started.TrySetResult();
                await release.Task;
            });
        await started.Task;

        var exception = await Assert.ThrowsExceptionAsync<OperationConflictException>(
            () => coordinator.RunAsync(
                OperationRequest.Create(
                    "second",
                    "Segunda operação",
                    "shared-resource"),
                (_, _) => Task.CompletedTask));

        Assert.AreEqual("shared-resource", exception.ResourceKey);
        Assert.AreEqual("Primeira operação", exception.ActiveOperationName);

        release.TrySetResult();
        Assert.AreEqual(
            OperationExecutionStatus.Succeeded,
            (await firstTask).Status);
    }

    [TestMethod]
    public async Task Cancel_StopsCooperativeOperationAndPersistsState()
    {
        var repository = new InMemoryOperationRepository();
        var coordinator = new OperationCoordinator(
            repository,
            new InMemoryDiagnosticLogger());
        var started = NewSignal();
        var request = OperationRequest.Create(
            "cancel",
            "Operação cancelável",
            "cancel-resource");

        var running = coordinator.RunAsync(
            request,
            async (_, cancellationToken) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        await started.Task;

        Assert.IsTrue(coordinator.Cancel(request.Id));
        var result = await running;

        Assert.AreEqual(OperationExecutionStatus.Cancelled, result.Status);
        Assert.AreEqual("OPERATION_CANCELLED", result.ErrorCode);
        Assert.AreEqual(result, repository.Get(request.Id));
        await WaitUntilAsync(
            () => !coordinator.IsResourceBusy("cancel-resource"),
            TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task Timeout_PersistsStateAndKeepsResourceUntilLateTaskEnds()
    {
        var coordinator = new OperationCoordinator(
            new InMemoryOperationRepository(),
            new InMemoryDiagnosticLogger());
        var release = NewSignal();
        var request = OperationRequest.Create(
            "timeout",
            "Operação sem cooperação",
            "timeout-resource",
            timeout: TimeSpan.FromMilliseconds(80));

        var result = await coordinator.RunAsync(
            request,
            async (_, _) => await release.Task);

        Assert.AreEqual(OperationExecutionStatus.TimedOut, result.Status);
        Assert.AreEqual("OPERATION_TIMEOUT", result.ErrorCode);
        Assert.IsTrue(coordinator.IsResourceBusy("timeout-resource"));

        release.TrySetResult();
        await WaitUntilAsync(
            () => !coordinator.IsResourceBusy("timeout-resource"),
            TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void RecoverInterruptedOperations_ClosesPendingAndRunningRecords()
    {
        var repository = new InMemoryOperationRepository();
        var now = DateTimeOffset.UtcNow;
        repository.Save(CreateExecution(
            OperationExecutionStatus.Pending,
            "pending",
            now));
        repository.Save(CreateExecution(
            OperationExecutionStatus.Running,
            "running",
            now.AddSeconds(1)));
        repository.Save(CreateExecution(
            OperationExecutionStatus.Succeeded,
            "succeeded",
            now.AddSeconds(2)));
        var coordinator = new OperationCoordinator(
            repository,
            new InMemoryDiagnosticLogger());

        var recovered = coordinator.RecoverInterruptedOperations();

        Assert.AreEqual(2, recovered);
        Assert.AreEqual(
            2,
            repository.GetRecent()
                .Count(item =>
                    item.Status == OperationExecutionStatus.Interrupted));
        Assert.AreEqual(
            1,
            repository.GetRecent()
                .Count(item =>
                    item.Status == OperationExecutionStatus.Succeeded));
    }

    private static OperationExecution CreateExecution(
        OperationExecutionStatus status,
        string type,
        DateTimeOffset createdAt) =>
        new(
            Guid.NewGuid(),
            type,
            type,
            type,
            null,
            status,
            status == OperationExecutionStatus.Succeeded ? 100 : 10,
            "Teste",
            createdAt,
            status == OperationExecutionStatus.Pending ? null : createdAt,
            status == OperationExecutionStatus.Succeeded ? createdAt : null,
            60,
            null,
            null);

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout)
    {
        var startedAt = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - startedAt > timeout)
            {
                Assert.Fail("A condição não foi atendida dentro do tempo esperado.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class InMemoryOperationRepository
        : IOperationExecutionRepository
    {
        private readonly object _sync = new();
        private readonly Dictionary<Guid, OperationExecution> _items = [];

        public OperationExecution? Get(Guid id)
        {
            lock (_sync)
            {
                return _items.GetValueOrDefault(id);
            }
        }

        public IReadOnlyList<OperationExecution> GetRecent(
            int maximumEntries = 50)
        {
            lock (_sync)
            {
                return _items.Values
                    .OrderByDescending(item => item.CreatedAt)
                    .Take(maximumEntries)
                    .ToArray();
            }
        }

        public void Save(OperationExecution execution)
        {
            lock (_sync)
            {
                _items[execution.Id] = execution;
            }
        }

        public int MarkIncompleteAsInterrupted(
            DateTimeOffset interruptedAt,
            string reason)
        {
            lock (_sync)
            {
                var incomplete = _items.Values
                    .Where(item =>
                        item.Status is OperationExecutionStatus.Pending
                            or OperationExecutionStatus.Running)
                    .ToArray();

                foreach (var item in incomplete)
                {
                    _items[item.Id] = item with
                    {
                        Status = OperationExecutionStatus.Interrupted,
                        CurrentStep = "Execução interrompida",
                        CompletedAt = interruptedAt,
                        ErrorCode = "PROCESS_INTERRUPTED",
                        ErrorMessage = reason
                    };
                }

                return incomplete.Length;
            }
        }
    }

    private sealed class InMemoryDiagnosticLogger : IDiagnosticLogger
    {
        public List<DiagnosticLogEntry> Entries { get; } = [];

        public string StoragePath => "memory://diagnostics";

        public IReadOnlyList<DiagnosticLogEntry> GetRecent(
            int maximumEntries = 100) =>
            Entries.TakeLast(maximumEntries).Reverse().ToArray();

        public void Write(
            DiagnosticLevel level,
            string source,
            string message,
            Exception? exception = null,
            IReadOnlyDictionary<string, string>? properties = null) =>
            Entries.Add(new DiagnosticLogEntry(
                DateTimeOffset.UtcNow,
                level,
                source,
                message,
                exception?.GetType().FullName,
                exception?.Message,
                exception?.StackTrace,
                properties ?? new Dictionary<string, string>()));
    }
}
