using System.Text;
using System.Text.Json;
using AutoAIBuilder.Application.Automation.Adapters;
using AutoAIBuilder.Application.Automation.Catalog;
using AutoAIBuilder.Application.Automation.Contracts;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Pilots;

namespace AutoAIBuilder.Infrastructure.Automation.Pilots;

public sealed class VerifiedCopyAutomationAdapter :
    IAutomationAdapter
{
    private static readonly JsonSerializerOptions ManifestSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

    private readonly TimeProvider _timeProvider;

    public VerifiedCopyAutomationAdapter(
        IAutomationContractSerializer serializer,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _timeProvider = timeProvider ?? TimeProvider.System;
        var mask = VerifiedCopyPilotContract.CreateMask();
        var catalog = VerifiedCopyPilotContract.CreateRuleCatalog();
        var fingerprint = AutomationMaskPackageFingerprint.Compute(
            serializer.Serialize(mask),
            serializer.Serialize(catalog));
        Descriptor = new AutomationAdapterDescriptor(
            VerifiedCopyPilotConstants.AdapterId,
            VerifiedCopyPilotConstants.AdapterVersion,
            "Cópia técnica verificada",
            "Adaptador interno do piloto que copia uma entrada isolada e "
            + "produz um manifesto de integridade.",
            "AutoAIBuilder",
            mask.Id,
            mask.Version,
            fingerprint,
            true,
            true,
            true,
            false,
            AutomationAdapterOrigin.BuiltIn);
    }

    public AutomationAdapterDescriptor Descriptor { get; }

    public async Task<AutomationAdapterResult> ExecuteAsync(
        AutomationWorkspaceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
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
