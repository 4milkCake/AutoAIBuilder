using System.Text.Json;
using System.Text.Json.Serialization;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Pilots;
using AutoAIBuilder.Application.Automation.Validation;

namespace AutoAIBuilder.Infrastructure.Automation.Pilots;

public sealed class VerifiedCopyPilotValidator :
    IAutomationExecutionValidator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public Task<IReadOnlyList<AutomationValidationIssue>> ValidateBeforeAsync(
        AutomationExecutionPlan plan,
        CancellationToken cancellationToken)
    {
        if (!IsPilot(plan))
        {
            return Task.FromResult<IReadOnlyList<AutomationValidationIssue>>([]);
        }

        var issues = new List<AutomationValidationIssue>();
        if (plan.Inputs.Count != 1)
        {
            issues.Add(AutomationValidationIssue.Error(
                "pré-validação",
                "pilot.single-input-required",
                "O piloto aceita exatamente um arquivo por execução."));
        }

        if (!plan.Parameters.TryGetValue(
                VerifiedCopyPilotConstants.SourceFileNameParameter,
                out var fileName)
            || !IsSafeFileName(fileName))
        {
            issues.Add(AutomationValidationIssue.Error(
                "pré-validação",
                "pilot.invalid-file-name",
                "O nome do arquivo de saída não é seguro."));
        }

        return Task.FromResult<IReadOnlyList<AutomationValidationIssue>>(issues);
    }

    public async Task<IReadOnlyList<AutomationValidationIssue>>
        ValidateAfterAsync(
            AutomationExecutionPlan plan,
            AutomationWorkspaceContext workspace,
            AutomationAdapterResult adapterResult,
            CancellationToken cancellationToken)
    {
        if (!IsPilot(plan))
        {
            return [];
        }

        try
        {
            var fileName = workspace.Parameters[
                VerifiedCopyPilotConstants.SourceFileNameParameter];
            var inputCopy = workspace.InputCopies.Single();
            var verifiedCopy = Path.Combine(
                workspace.ResultDirectory,
                VerifiedCopyPilotConstants.VerifiedFilesDirectory,
                fileName);
            var manifestPath = Path.Combine(
                workspace.ResultDirectory,
                VerifiedCopyPilotConstants.ManifestFileName);

            if (!File.Exists(verifiedCopy) || !File.Exists(manifestPath))
            {
                return
                [
                    AutomationValidationIssue.Error(
                        "pós-validação",
                        "pilot.output-missing",
                        "A cópia verificada ou o manifesto não foi produzido.")
                ];
            }

            var inputChecksum = await FileChecksum
                .ComputeSha256Async(inputCopy, cancellationToken)
                .ConfigureAwait(false);
            var outputChecksum = await FileChecksum
                .ComputeSha256Async(verifiedCopy, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(
                    inputChecksum,
                    outputChecksum,
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                [
                    AutomationValidationIssue.Error(
                        "pós-validação",
                        "pilot.copy-checksum-mismatch",
                        "O SHA-256 da cópia publicada difere da entrada isolada.")
                ];
            }

            var json = await File.ReadAllTextAsync(
                    manifestPath,
                    cancellationToken)
                .ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<VerifiedCopyManifest>(
                json,
                SerializerOptions);
            if (manifest is null
                || manifest.SchemaVersion != "1.0"
                || manifest.ExecutionId != workspace.ExecutionId
                || manifest.PilotId != VerifiedCopyPilotConstants.MaskId
                || manifest.PilotVersion != VerifiedCopyPilotConstants.MaskVersion
                || !string.Equals(
                    manifest.SourceFileName,
                    fileName,
                    StringComparison.Ordinal)
                || !string.Equals(
                    manifest.SourceCopySha256,
                    inputChecksum,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    manifest.VerifiedCopySha256,
                    outputChecksum,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    NormalizeRelativePath(manifest.VerifiedCopyRelativePath),
                    NormalizeRelativePath(Path.Combine(
                        VerifiedCopyPilotConstants.VerifiedFilesDirectory,
                        fileName)),
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                [
                    AutomationValidationIssue.Error(
                        "pós-validação",
                        "pilot.manifest-mismatch",
                        "O manifesto não corresponde à identidade ou aos "
                        + "checksums da execução.")
                ];
            }

            return [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or KeyNotFoundException
                or JsonException)
        {
            return
            [
                AutomationValidationIssue.Error(
                    "pós-validação",
                    "pilot.post-validation-failed",
                    $"Não foi possível validar a saída do piloto: "
                    + exception.Message)
            ];
        }
    }

    internal static bool IsSafeFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && string.Equals(
            fileName,
            Path.GetFileName(fileName),
            StringComparison.Ordinal)
        && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && fileName is not "." and not "..";

    private static bool IsPilot(AutomationExecutionPlan plan) =>
        string.Equals(
            plan.Mask.Id,
            VerifiedCopyPilotConstants.MaskId,
            StringComparison.Ordinal)
        && string.Equals(
            plan.Mask.Version,
            VerifiedCopyPilotConstants.MaskVersion,
            StringComparison.Ordinal);

    private static string NormalizeRelativePath(string value) =>
        value.Replace('\\', '/');
}
