using AutoAIBuilder.Application.Automation.Execution;

namespace AutoAIBuilder.Infrastructure.Automation;

internal sealed class SafeAutomationWorkspace
{
    public static string GetStagingPath(string outputRoot, Guid executionId) =>
        Path.Combine(
            outputRoot,
            ".autoaibuilder",
            "staging",
            executionId.ToString("N"));

    public async Task<PreparedAutomationWorkspace> PrepareAsync(
        AutomationExecutionPlan plan,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        var stagingRoot = GetStagingPath(plan.OutputRoot, executionId);
        if (Directory.Exists(stagingRoot))
        {
            throw new IOException(
                $"A área de trabalho '{stagingRoot}' já existe e foi preservada.");
        }

        var inputDirectory = Path.Combine(stagingRoot, "inputs");
        var resultDirectory = Path.Combine(stagingRoot, "result");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(resultDirectory);

        var copies = new List<string>();
        try
        {
            for (var index = 0; index < plan.Inputs.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var input = plan.Inputs[index];
                await EnsureOriginalMatchesAsync(input, cancellationToken)
                    .ConfigureAwait(false);

                var copyPath = Path.Combine(
                    inputDirectory,
                    $"{index + 1:000}-{Path.GetFileName(input.OriginalPath)}");
                await CopyNewAsync(
                        input.OriginalPath,
                        copyPath,
                        cancellationToken)
                    .ConfigureAwait(false);

                var copiedChecksum = await FileChecksum
                    .ComputeSha256Async(copyPath, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.Equals(
                        copiedChecksum,
                        input.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"A cópia de '{input.OriginalPath}' não corresponde ao original.");
                }

                await EnsureOriginalMatchesAsync(input, cancellationToken)
                    .ConfigureAwait(false);
                copies.Add(copyPath);
            }

            return new PreparedAutomationWorkspace(
                stagingRoot,
                resultDirectory,
                copies);
        }
        catch
        {
            // A área parcial é deliberadamente preservada para recuperação.
            throw;
        }
    }

    public string Publish(
        PreparedAutomationWorkspace workspace,
        AutomationExecutionPlan plan,
        Guid executionId,
        DateTimeOffset timestamp)
    {
        var safeMaskId = string.Concat(
            plan.Mask.Id.Select(
                character => char.IsLetterOrDigit(character)
                    || character is '-' or '.'
                        ? character
                        : '-'));
        var publishedPath = Path.Combine(
            plan.OutputRoot,
            $"AutoAIBuilder-{safeMaskId}-{timestamp:yyyyMMdd-HHmmss}-"
            + executionId.ToString("N")[..8]);

        if (Directory.Exists(publishedPath) || File.Exists(publishedPath))
        {
            throw new IOException(
                $"O destino de publicação '{publishedPath}' já existe.");
        }

        Directory.Move(workspace.StagingRoot, publishedPath);
        return publishedPath;
    }

    public string? PreserveForRecovery(
        string outputRoot,
        Guid executionId,
        string? stagingPath,
        string? publishedPath,
        DateTimeOffset timestamp)
    {
        var source = Directory.Exists(stagingPath)
            ? stagingPath
            : Directory.Exists(publishedPath)
                ? publishedPath
                : null;
        if (source is null)
        {
            return null;
        }

        var recoveryRoot = Path.Combine(
            outputRoot,
            ".autoaibuilder",
            "recovery");
        Directory.CreateDirectory(recoveryRoot);
        var recoveryPath = Path.Combine(
            recoveryRoot,
            $"{timestamp:yyyyMMdd-HHmmss}-{executionId:N}");
        if (Directory.Exists(recoveryPath) || File.Exists(recoveryPath))
        {
            throw new IOException(
                $"O destino de recuperação '{recoveryPath}' já existe.");
        }

        Directory.Move(source, recoveryPath);
        return recoveryPath;
    }

    public static async Task EnsureOriginalMatchesAsync(
        AutomationInputSnapshot input,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(input.OriginalPath))
        {
            throw new InvalidDataException(
                $"O arquivo original deixou de existir: {input.OriginalPath}");
        }

        var checksum = await FileChecksum
            .ComputeSha256Async(input.OriginalPath, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(
                checksum,
                input.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"O checksum do original mudou desde a simulação: "
                + input.OriginalPath);
        }
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

internal sealed record PreparedAutomationWorkspace(
    string StagingRoot,
    string ResultDirectory,
    IReadOnlyList<string> InputCopies);
