using System.Text;
using System.Text.Json;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Pilots;

namespace AutoAIBuilder.Infrastructure.Automation.Pilots;

public sealed class VerifiedCopyPilotService(
    IAutomationPlanService planService,
    IAutomationExecutionService executionService,
    IAutomationAuditRepository auditRepository,
    TimeProvider? timeProvider = null) : IVerifiedCopyPilotService
{
    private static readonly JsonSerializerOptions ManifestSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

    private readonly TimeProvider _timeProvider =
        timeProvider ?? TimeProvider.System;

    public async Task<VerifiedCopySimulation> SimulateAsync(
        VerifiedCopyPilotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceFileName = string.IsNullOrWhiteSpace(request.InputPath)
            ? string.Empty
            : Path.GetFileName(request.InputPath);
        var plan = await planService.CreatePlanAsync(
                new AutomationPlanRequest(
                    request.ProjectId,
                    VerifiedCopyPilotContract.CreateRuleCatalog(),
                    VerifiedCopyPilotContract.CreateMask(),
                    [request.InputPath],
                    request.OutputRoot,
                    new Dictionary<string, string>
                    {
                        [VerifiedCopyPilotConstants.SourceFileNameParameter] =
                            sourceFileName
                    }),
                AutomationExecutionMode.Simulation,
                cancellationToken)
            .ConfigureAwait(false);

        if (request.ProjectValidationIssues.Count > 0)
        {
            plan = plan with
            {
                ValidationIssues =
                [
                    .. plan.ValidationIssues,
                    .. request.ProjectValidationIssues
                ]
            };
        }

        var outcome = await executionService.ExecuteAsync(
                plan,
                (_, _) => Task.FromResult(
                    AutomationAdapterResult.Failure(
                        "O adaptador não deve ser chamado durante a simulação.")),
                cancellationToken)
            .ConfigureAwait(false);
        return new VerifiedCopySimulation(plan, outcome);
    }

    public Task<AutomationExecutionOutcome> ExecuteAsync(
        AutomationExecutionPlan simulatedPlan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(simulatedPlan);
        if (simulatedPlan.Mode != AutomationExecutionMode.Simulation
            || !string.Equals(
                simulatedPlan.Mask.Id,
                VerifiedCopyPilotConstants.MaskId,
                StringComparison.Ordinal)
            || !string.Equals(
                simulatedPlan.Mask.Version,
                VerifiedCopyPilotConstants.MaskVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "O plano não pertence ao piloto de cópia técnica verificada.");
        }

        var simulationAudit = auditRepository.GetLatestByPlanId(
            simulatedPlan.Id);
        if (simulationAudit?.Status != AutomationAuditStatus.Simulated)
        {
            throw new InvalidOperationException(
                "Execute e aprove uma nova simulação antes de aplicar o piloto.");
        }

        var applyPlan = simulatedPlan with
        {
            Id = Guid.NewGuid(),
            Mode = AutomationExecutionMode.Apply,
            Actions = simulatedPlan.Actions
                .Where(action => action.Code != "simulation")
                .ToArray(),
            CreatedAt = _timeProvider.GetUtcNow()
        };
        return executionService.ExecuteAsync(
            applyPlan,
            ExecuteAdapterAsync,
            cancellationToken);
    }

    private async Task<AutomationAdapterResult> ExecuteAdapterAsync(
        AutomationWorkspaceContext context,
        CancellationToken cancellationToken)
    {
        if (context.InputCopies.Count != 1
            || !context.Parameters.TryGetValue(
                VerifiedCopyPilotConstants.SourceFileNameParameter,
                out var fileName)
            || !VerifiedCopyPilotValidator.IsSafeFileName(fileName))
        {
            return AutomationAdapterResult.Failure(
                "A entrada isolada ou o nome da cópia é inválido.");
        }

        var inputCopy = context.InputCopies.Single();
        var verifiedDirectory = Path.Combine(
            context.ResultDirectory,
            VerifiedCopyPilotConstants.VerifiedFilesDirectory);
        Directory.CreateDirectory(verifiedDirectory);
        var verifiedCopy = Path.Combine(verifiedDirectory, fileName);

        await CopyNewAsync(
                inputCopy,
                verifiedCopy,
                cancellationToken)
            .ConfigureAwait(false);
        var inputChecksum = await FileChecksum
            .ComputeSha256Async(inputCopy, cancellationToken)
            .ConfigureAwait(false);
        var verifiedChecksum = await FileChecksum
            .ComputeSha256Async(verifiedCopy, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(
                inputChecksum,
                verifiedChecksum,
                StringComparison.OrdinalIgnoreCase))
        {
            return AutomationAdapterResult.Failure(
                "A cópia técnica não corresponde à entrada isolada.");
        }

        var manifest = new VerifiedCopyManifest(
            "1.0",
            context.ExecutionId,
            VerifiedCopyPilotConstants.MaskId,
            VerifiedCopyPilotConstants.MaskVersion,
            fileName,
            inputChecksum,
            Path.Combine(
                VerifiedCopyPilotConstants.VerifiedFilesDirectory,
                fileName).Replace('\\', '/'),
            verifiedChecksum,
            _timeProvider.GetUtcNow());
        var manifestPath = Path.Combine(
            context.ResultDirectory,
            VerifiedCopyPilotConstants.ManifestFileName);
        var json = JsonSerializer.Serialize(
            manifest,
            ManifestSerializerOptions);
        await File.WriteAllTextAsync(
                manifestPath,
                json,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken)
            .ConfigureAwait(false);

        return AutomationAdapterResult.Success(
            "Cópia técnica e manifesto publicados com checksums equivalentes.",
            [verifiedCopy, manifestPath],
            [
                $"SHA-256 da entrada isolada: {inputChecksum}",
                $"SHA-256 da cópia verificada: {verifiedChecksum}"
            ]);
    }

    private static async Task CopyNewAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 131_072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 131_072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(
                destination,
                bufferSize: 131_072,
                cancellationToken)
            .ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
