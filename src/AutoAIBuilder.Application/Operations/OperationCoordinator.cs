using AutoAIBuilder.Application.Diagnostics;

namespace AutoAIBuilder.Application.Operations;

public sealed class OperationCoordinator : IOperationCoordinator
{
    private const int MaximumPersistedErrorLength = 2_000;

    private readonly IOperationExecutionRepository _repository;
    private readonly IDiagnosticLogger _diagnosticLogger;
    private readonly TimeProvider _timeProvider;
    private readonly object _sync = new();
    private readonly Dictionary<Guid, ActiveOperation> _activeById = [];
    private readonly Dictionary<string, ActiveOperation> _activeByResource =
        new(StringComparer.OrdinalIgnoreCase);

    public OperationCoordinator(
        IOperationExecutionRepository repository,
        IDiagnosticLogger diagnosticLogger,
        TimeProvider? timeProvider = null)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        _diagnosticLogger = diagnosticLogger
            ?? throw new ArgumentNullException(nameof(diagnosticLogger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<OperationExecution> RunAsync(
        OperationRequest request,
        Func<OperationContext, CancellationToken, Task> operation,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(operation);

        var activeOperation = Reserve(request);
        var stateSync = new object();
        var createdAt = _timeProvider.GetUtcNow();
        var current = new OperationExecution(
            request.Id,
            request.OperationType,
            request.DisplayName,
            request.ResourceKey,
            request.ProjectId,
            OperationExecutionStatus.Pending,
            0,
            "Aguardando início",
            createdAt,
            null,
            null,
            checked((int)Math.Ceiling(request.Timeout.TotalSeconds)),
            null,
            null);
        Task? operationTask = null;
        var releaseImmediately = true;

        try
        {
            Persist(current);
            current = current with
            {
                Status = OperationExecutionStatus.Running,
                CurrentStep = "Operação iniciada",
                StartedAt = _timeProvider.GetUtcNow()
            };
            Persist(current);
            TryWriteDiagnostic(
                DiagnosticLevel.Information,
                "Operação iniciada.",
                current);

            void ReportProgress(OperationProgress update)
            {
                lock (stateSync)
                {
                    if (current.Status != OperationExecutionStatus.Running)
                    {
                        return;
                    }

                    current = current with
                    {
                        Progress = Math.Max(current.Progress, update.Percentage),
                        CurrentStep = Limit(update.Message, 500)
                    };
                    Persist(current);
                }

                TryReport(progress, update);
            }

            var context = new OperationContext(request.Id, ReportProgress);
            using var linkedCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken,
                    activeOperation.Cancellation.Token);

            operationTask = operation(context, linkedCancellation.Token)
                ?? throw new InvalidOperationException(
                    "A operação assíncrona não retornou uma tarefa.");

            await operationTask
                .WaitAsync(request.Timeout, linkedCancellation.Token)
                .ConfigureAwait(false);

            linkedCancellation.Token.ThrowIfCancellationRequested();
            lock (stateSync)
            {
                current = Complete(
                    current,
                    OperationExecutionStatus.Succeeded,
                    100,
                    "Operação concluída",
                    null,
                    null);
            }

            TryReport(
                progress,
                OperationProgress.Create(100, "Operação concluída"));
            TryWriteDiagnostic(
                DiagnosticLevel.Information,
                "Operação concluída com sucesso.",
                current);
            return current;
        }
        catch (TimeoutException)
        {
            activeOperation.Cancellation.Cancel();
            lock (stateSync)
            {
                current = Complete(
                    current,
                    OperationExecutionStatus.TimedOut,
                    current.Progress,
                    "Tempo limite excedido",
                    "OPERATION_TIMEOUT",
                    $"A operação excedeu o limite de {request.Timeout}.");
            }

            TryWriteDiagnostic(
                DiagnosticLevel.Warning,
                "Operação interrompida por timeout.",
                current);
            releaseImmediately = operationTask?.IsCompleted ?? true;
            return current;
        }
        catch (OperationCanceledException)
        {
            activeOperation.Cancellation.Cancel();
            lock (stateSync)
            {
                current = Complete(
                    current,
                    OperationExecutionStatus.Cancelled,
                    current.Progress,
                    "Operação cancelada",
                    "OPERATION_CANCELLED",
                    "A operação foi cancelada antes da conclusão.");
            }

            TryWriteDiagnostic(
                DiagnosticLevel.Information,
                "Operação cancelada.",
                current);
            releaseImmediately = operationTask?.IsCompleted ?? true;
            return current;
        }
        catch (Exception exception)
        {
            lock (stateSync)
            {
                current = Complete(
                    current,
                    OperationExecutionStatus.Failed,
                    current.Progress,
                    "Falha na operação",
                    exception.GetType().Name,
                    Limit(exception.Message, MaximumPersistedErrorLength));
            }

            TryWriteDiagnostic(
                DiagnosticLevel.Error,
                "Operação concluída com falha isolada.",
                current,
                exception);
            return current;
        }
        finally
        {
            if (releaseImmediately)
            {
                Release(activeOperation);
            }
            else if (operationTask is not null)
            {
                _ = ObserveLateCompletionAsync(operationTask, activeOperation);
            }
        }
    }

    public bool Cancel(Guid executionId)
    {
        ActiveOperation? activeOperation;
        lock (_sync)
        {
            _activeById.TryGetValue(executionId, out activeOperation);
        }

        if (activeOperation is null)
        {
            return false;
        }

        try
        {
            activeOperation.Cancellation.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public bool IsResourceBusy(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return false;
        }

        lock (_sync)
        {
            return _activeByResource.ContainsKey(resourceKey.Trim());
        }
    }

    public IReadOnlyList<OperationExecution> GetRecent(int maximumEntries = 50) =>
        _repository.GetRecent(maximumEntries);

    public int RecoverInterruptedOperations()
    {
        var recovered = _repository.MarkIncompleteAsInterrupted(
            _timeProvider.GetUtcNow(),
            "A execução foi interrompida pelo encerramento anterior do aplicativo.");

        if (recovered > 0)
        {
            TryWriteDiagnostic(
                DiagnosticLevel.Warning,
                "Execuções incompletas foram marcadas como interrompidas.",
                properties: new Dictionary<string, string>
                {
                    ["count"] = recovered.ToString()
                });
        }

        return recovered;
    }

    private ActiveOperation Reserve(OperationRequest request)
    {
        lock (_sync)
        {
            if (_activeById.ContainsKey(request.Id))
            {
                throw new InvalidOperationException(
                    $"Já existe uma execução ativa com o identificador {request.Id}.");
            }

            if (_activeByResource.TryGetValue(
                    request.ResourceKey,
                    out var existing))
            {
                throw new OperationConflictException(
                    request.ResourceKey,
                    existing.DisplayName);
            }

            var active = new ActiveOperation(
                request.Id,
                request.ResourceKey,
                request.DisplayName,
                new CancellationTokenSource());
            _activeById.Add(request.Id, active);
            _activeByResource.Add(request.ResourceKey, active);
            return active;
        }
    }

    private void Release(ActiveOperation activeOperation)
    {
        lock (_sync)
        {
            _activeById.Remove(activeOperation.Id);
            if (_activeByResource.TryGetValue(
                    activeOperation.ResourceKey,
                    out var current)
                && ReferenceEquals(current, activeOperation))
            {
                _activeByResource.Remove(activeOperation.ResourceKey);
            }
        }

        activeOperation.Cancellation.Dispose();
    }

    private async Task ObserveLateCompletionAsync(
        Task operationTask,
        ActiveOperation activeOperation)
    {
        try
        {
            await operationTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // O estado terminal já foi persistido pelo fluxo principal.
        }
        catch (Exception exception)
        {
            TryWriteDiagnostic(
                DiagnosticLevel.Warning,
                "Uma tarefa encerrou com erro depois do cancelamento ou timeout.",
                exception: exception,
                properties: new Dictionary<string, string>
                {
                    ["executionId"] = activeOperation.Id.ToString()
                });
        }
        finally
        {
            Release(activeOperation);
        }
    }

    private OperationExecution Complete(
        OperationExecution execution,
        OperationExecutionStatus status,
        int progress,
        string currentStep,
        string? errorCode,
        string? errorMessage)
    {
        var completed = execution with
        {
            Status = status,
            Progress = progress,
            CurrentStep = currentStep,
            CompletedAt = _timeProvider.GetUtcNow(),
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
        Persist(completed);
        return completed;
    }

    private void Persist(OperationExecution execution) =>
        _repository.Save(execution);

    private static void TryReport(
        IProgress<OperationProgress>? progress,
        OperationProgress update)
    {
        try
        {
            progress?.Report(update);
        }
        catch
        {
            // Um observador visual não pode interromper a operação.
        }
    }

    private void TryWriteDiagnostic(
        DiagnosticLevel level,
        string message,
        OperationExecution? execution = null,
        Exception? exception = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        try
        {
            var diagnosticProperties = properties is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(properties);

            if (execution is not null)
            {
                diagnosticProperties["executionId"] = execution.Id.ToString();
                diagnosticProperties["operationType"] = execution.OperationType;
                diagnosticProperties["resourceKey"] = execution.ResourceKey;
                diagnosticProperties["status"] = execution.Status.ToString();
            }

            _diagnosticLogger.Write(
                level,
                "OperationEngine",
                message,
                exception,
                diagnosticProperties);
        }
        catch
        {
            // O diagnóstico nunca deve alterar o resultado operacional.
        }
    }

    private static string Limit(string value, int maximumLength) =>
        value.Length <= maximumLength
            ? value
            : value[..maximumLength];

    private sealed record ActiveOperation(
        Guid Id,
        string ResourceKey,
        string DisplayName,
        CancellationTokenSource Cancellation);
}
