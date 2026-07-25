using System.Collections.Concurrent;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Validation;

namespace AutoAIBuilder.Infrastructure.Automation;

public sealed class AutomationExecutionService : IAutomationExecutionService
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private readonly IAutomationAuditRepository _auditRepository;
    private readonly IReadOnlyList<IAutomationExecutionValidator> _validators;
    private readonly SafeAutomationWorkspace _workspace = new();
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _idempotencyLocks =
        new(StringComparer.Ordinal);

    public AutomationExecutionService(
        IAutomationAuditRepository auditRepository,
        IEnumerable<IAutomationExecutionValidator>? validators = null,
        TimeProvider? timeProvider = null)
    {
        _auditRepository = auditRepository
            ?? throw new ArgumentNullException(nameof(auditRepository));
        _validators = validators?.ToArray() ?? [];
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AutomationExecutionOutcome> ExecuteAsync(
        AutomationExecutionPlan plan,
        Func<AutomationWorkspaceContext, CancellationToken, Task<AutomationAdapterResult>>
            adapter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(adapter);

        if (!plan.IsValid)
        {
            return Reject(
                plan,
                plan.ValidationIssues,
                "O plano foi rejeitado pela pré-validação.");
        }

        if (plan.Mode == AutomationExecutionMode.Simulation)
        {
            var simulationIssues = plan.ValidationIssues
                .Concat(
                    await ValidateBeforeAsync(plan, cancellationToken)
                        .ConfigureAwait(false))
                .ToArray();
            if (HasErrors(simulationIssues))
            {
                return Reject(
                    plan,
                    simulationIssues,
                    "A simulação foi bloqueada pela pré-validação.");
            }

            var audit = CreateAudit(
                plan,
                AutomationAuditStatus.Simulated,
                "Simulação concluída sem criar cópias ou arquivos de saída.",
                completedAt: _timeProvider.GetUtcNow());
            _auditRepository.Save(audit);
            return ToOutcome(audit, simulationIssues);
        }

        var keyLock = _idempotencyLocks.GetOrAdd(
            plan.IdempotencyKey,
            _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var previous = plan.Mask.IsIdempotent
                ? _auditRepository.GetSuccessfulByIdempotencyKey(
                    plan.IdempotencyKey)
                : null;
            if (previous is not null)
            {
                var reused = CreateAudit(
                    plan,
                    AutomationAuditStatus.Reused,
                    $"Resultado idempotente reutilizado da auditoria {previous.Id}.",
                    previous.OutputPaths,
                    previous.PublishedPath,
                    completedAt: _timeProvider.GetUtcNow());
                _auditRepository.Save(reused);
                return ToOutcome(reused, [], reusedPreviousResult: true);
            }

            return await ExecuteApplyAsync(plan, adapter, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            keyLock.Release();
        }
    }

    private async Task<AutomationExecutionOutcome> ExecuteApplyAsync(
        AutomationExecutionPlan plan,
        Func<AutomationWorkspaceContext, CancellationToken, Task<AutomationAdapterResult>>
            adapter,
        CancellationToken cancellationToken)
    {
        var auditId = Guid.NewGuid();
        var startedAt = _timeProvider.GetUtcNow();
        var currentAudit = CreateAudit(
            plan,
            AutomationAuditStatus.Running,
            "Execução iniciada sobre área de trabalho isolada.",
            id: auditId,
            createdAt: startedAt);
        _auditRepository.Save(currentAudit);

        PreparedAutomationWorkspace? prepared = null;
        string? publishedPath = null;
        var validationIssues = new List<AutomationValidationIssue>();
        try
        {
            foreach (var input in plan.Inputs)
            {
                await SafeAutomationWorkspace
                    .EnsureOriginalMatchesAsync(input, cancellationToken)
                    .ConfigureAwait(false);
            }

            validationIssues.AddRange(
                await ValidateBeforeAsync(plan, cancellationToken)
                    .ConfigureAwait(false));
            if (HasErrors(validationIssues))
            {
                return RejectRunning(
                    currentAudit,
                    validationIssues,
                    "A execução foi bloqueada pela pré-validação.");
            }

            prepared = await _workspace
                .PrepareAsync(plan, auditId, cancellationToken)
                .ConfigureAwait(false);
            var context = new AutomationWorkspaceContext(
                auditId,
                prepared.StagingRoot,
                prepared.ResultDirectory,
                prepared.InputCopies,
                plan.Parameters);
            var adapterResult = await adapter(context, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "O adaptador não retornou um resultado.");

            if (!adapterResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(adapterResult.Summary)
                        ? "O adaptador informou uma falha."
                        : adapterResult.Summary);
            }

            var stagedOutputs = ValidateOutputs(
                plan,
                context,
                adapterResult,
                validationIssues);
            validationIssues.AddRange(
                await ValidateAfterAsync(
                        plan,
                        context,
                        adapterResult,
                        cancellationToken)
                    .ConfigureAwait(false));

            foreach (var input in plan.Inputs)
            {
                await SafeAutomationWorkspace
                    .EnsureOriginalMatchesAsync(input, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (HasErrors(validationIssues))
            {
                throw new InvalidDataException(
                    "A pós-validação encontrou erros na saída.");
            }

            publishedPath = _workspace.Publish(
                prepared,
                plan,
                auditId,
                _timeProvider.GetUtcNow());
            var publishedOutputs = stagedOutputs
                .Select(
                    path => Path.Combine(
                        publishedPath,
                        Path.GetRelativePath(prepared.StagingRoot, path)))
                .ToArray();
            var completed = currentAudit with
            {
                Status = AutomationAuditStatus.Succeeded,
                OutputPaths = publishedOutputs,
                PublishedPath = publishedPath,
                Summary = string.IsNullOrWhiteSpace(adapterResult.Summary)
                    ? "Automação concluída e saída validada."
                    : adapterResult.Summary.Trim(),
                CompletedAt = _timeProvider.GetUtcNow()
            };
            _auditRepository.Save(completed);
            return ToOutcome(completed, validationIssues);
        }
        catch (OperationCanceledException)
        {
            var recoveryPath = PreserveSafely(
                plan,
                auditId,
                prepared?.StagingRoot
                    ?? SafeAutomationWorkspace.GetStagingPath(
                        plan.OutputRoot,
                        auditId),
                publishedPath);
            var cancelled = currentAudit with
            {
                Status = AutomationAuditStatus.CancelledRolledBack,
                RecoveryPath = recoveryPath,
                Summary = recoveryPath is null
                    ? "Execução cancelada antes de criar artefatos."
                    : "Execução cancelada; artefatos parciais preservados para recuperação.",
                CompletedAt = _timeProvider.GetUtcNow()
            };
            _auditRepository.Save(cancelled);
            throw;
        }
        catch (Exception exception)
        {
            var recoveryPath = PreserveSafely(
                plan,
                auditId,
                prepared?.StagingRoot
                    ?? SafeAutomationWorkspace.GetStagingPath(
                        plan.OutputRoot,
                        auditId),
                publishedPath);
            validationIssues.Add(AutomationValidationIssue.Error(
                "execução",
                "execution.failed",
                exception.Message));
            var failed = currentAudit with
            {
                Status = AutomationAuditStatus.FailedRolledBack,
                RecoveryPath = recoveryPath,
                Summary = recoveryPath is null
                    ? $"Execução falhou sem alterar saídas: {exception.Message}"
                    : "Execução falhou; artefatos parciais foram preservados "
                      + $"em '{recoveryPath}'.",
                CompletedAt = _timeProvider.GetUtcNow()
            };
            _auditRepository.Save(failed);
            return ToOutcome(failed, validationIssues);
        }
    }

    private AutomationExecutionOutcome Reject(
        AutomationExecutionPlan plan,
        IReadOnlyList<AutomationValidationIssue> issues,
        string summary)
    {
        var audit = CreateAudit(
            plan,
            AutomationAuditStatus.Rejected,
            summary,
            completedAt: _timeProvider.GetUtcNow());
        _auditRepository.Save(audit);
        return ToOutcome(audit, issues);
    }

    private AutomationExecutionOutcome RejectRunning(
        AutomationAuditEntry audit,
        IReadOnlyList<AutomationValidationIssue> issues,
        string summary)
    {
        var rejected = audit with
        {
            Status = AutomationAuditStatus.Rejected,
            Summary = summary,
            CompletedAt = _timeProvider.GetUtcNow()
        };
        _auditRepository.Save(rejected);
        return ToOutcome(rejected, issues);
    }

    private async Task<IReadOnlyList<AutomationValidationIssue>>
        ValidateBeforeAsync(
            AutomationExecutionPlan plan,
            CancellationToken cancellationToken)
    {
        var issues = new List<AutomationValidationIssue>();
        foreach (var validator in _validators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            issues.AddRange(
                await validator
                    .ValidateBeforeAsync(plan, cancellationToken)
                    .ConfigureAwait(false));
        }

        return issues;
    }

    private async Task<IReadOnlyList<AutomationValidationIssue>>
        ValidateAfterAsync(
            AutomationExecutionPlan plan,
            AutomationWorkspaceContext context,
            AutomationAdapterResult result,
            CancellationToken cancellationToken)
    {
        var issues = new List<AutomationValidationIssue>();
        foreach (var validator in _validators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            issues.AddRange(
                await validator
                    .ValidateAfterAsync(
                        plan,
                        context,
                        result,
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        return issues;
    }

    private static IReadOnlyList<string> ValidateOutputs(
        AutomationExecutionPlan plan,
        AutomationWorkspaceContext context,
        AutomationAdapterResult result,
        ICollection<AutomationValidationIssue> issues)
    {
        var outputs = new List<string>();
        foreach (var suppliedPath in result.OutputPaths ?? [])
        {
            var fullPath = Path.GetFullPath(
                Path.IsPathRooted(suppliedPath)
                    ? suppliedPath
                    : Path.Combine(context.ResultDirectory, suppliedPath));
            if (!IsWithin(context.WorkspaceRoot, fullPath))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pós-validação",
                    "output.outside-workspace",
                    $"A saída '{suppliedPath}' está fora da área isolada."));
                continue;
            }

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pós-validação",
                    "output.not-found",
                    $"A saída declarada não foi encontrada: {suppliedPath}"));
                continue;
            }

            outputs.Add(fullPath);
        }

        foreach (var expected in plan.Mask.Outputs.Where(output => output.IsRequired))
        {
            var expectedPath = Path.GetFullPath(
                Path.Combine(context.ResultDirectory, expected.RelativePath));
            if (!IsWithin(context.ResultDirectory, expectedPath)
                || (!File.Exists(expectedPath) && !Directory.Exists(expectedPath)))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pós-validação",
                    "output.required-missing",
                    $"A saída obrigatória '{expected.Id}' não foi produzida em "
                    + $"'{expected.RelativePath}'."));
            }
            else
            {
                outputs.Add(expectedPath);
            }
        }

        return outputs.Distinct(
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal).ToArray();
    }

    private string? PreserveSafely(
        AutomationExecutionPlan plan,
        Guid auditId,
        string? stagingPath,
        string? publishedPath)
    {
        try
        {
            return _workspace.PreserveForRecovery(
                plan.OutputRoot,
                auditId,
                stagingPath,
                publishedPath,
                _timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return $"PRESERVAÇÃO MANUAL NECESSÁRIA: "
                   + $"{stagingPath ?? publishedPath}; {exception.Message}";
        }
    }

    private AutomationAuditEntry CreateAudit(
        AutomationExecutionPlan plan,
        AutomationAuditStatus status,
        string summary,
        IReadOnlyList<string>? outputPaths = null,
        string? publishedPath = null,
        string? recoveryPath = null,
        DateTimeOffset? completedAt = null,
        Guid? id = null,
        DateTimeOffset? createdAt = null) =>
        new(
            id ?? Guid.NewGuid(),
            plan.Id,
            plan.ProjectId,
            plan.Mask.Id,
            plan.Mask.Version,
            plan.Mode,
            status,
            plan.IdempotencyKey,
            plan.Inputs,
            outputPaths ?? [],
            publishedPath,
            recoveryPath,
            summary,
            createdAt ?? _timeProvider.GetUtcNow(),
            completedAt);

    private static AutomationExecutionOutcome ToOutcome(
        AutomationAuditEntry audit,
        IReadOnlyList<AutomationValidationIssue> issues,
        bool reusedPreviousResult = false) =>
        new(
            audit.Id,
            audit.Status,
            audit.Summary,
            audit.PublishedPath,
            audit.RecoveryPath,
            audit.OutputPaths,
            issues,
            reusedPreviousResult);

    private static bool HasErrors(
        IEnumerable<AutomationValidationIssue> issues) =>
        issues.Any(
            issue => issue.Severity == AutomationValidationSeverity.Error);

    private static bool IsWithin(string root, string path)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(root));
        var normalizedPath = Path.GetFullPath(path);
        if (string.Equals(normalizedRoot, normalizedPath, PathComparison))
        {
            return true;
        }

        return normalizedPath.StartsWith(
            normalizedRoot + Path.DirectorySeparatorChar,
            PathComparison);
    }
}
