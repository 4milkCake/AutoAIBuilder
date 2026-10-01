using System.Security.Cryptography;
using System.Text.Json;
using AutoAIBuilder.Application.CadVisualization;
using AutoAIBuilder.Infrastructure.Automation;
using AutoAIBuilder.Infrastructure.Persistence;

namespace AutoAIBuilder.Infrastructure.CadVisualization;

public sealed class CadVisualizationService : ICadVisualizationService
{
    private const string PipelineVersion = "6";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ICadGeometryExporter _exporter;
    private readonly CadGeometryArtifactParser _parser;
    private readonly string _storageRoot;
    private readonly TimeProvider _timeProvider;

    public CadVisualizationService(
        ICadGeometryExporter exporter,
        CadGeometryArtifactParser? parser = null,
        string? storageRoot = null,
        TimeProvider? timeProvider = null)
    {
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _parser = parser ?? new CadGeometryArtifactParser();
        _storageRoot = storageRoot
            ?? AppStoragePaths.CadVisualizationDirectory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public CadVisualizationEngineStatus GetEngineStatus()
    {
        var status = _exporter.GetStatus();
        return new CadVisualizationEngineStatus(
            status.IsAvailable,
            status.Name,
            status.Version,
            status.ExecutablePath,
            status.Publisher,
            status.Message);
    }

    public CadVisualizationSnapshot? TryLoadCached(
        Guid projectId,
        string sourceDwgPath)
    {
        var sourcePath = ValidateSourcePath(sourceDwgPath);
        try
        {
            var checksum = ComputeSha256(sourcePath);
            var snapshotPath = GetSnapshotPath(projectId, checksum);
            if (!File.Exists(snapshotPath))
            {
                return null;
            }

            var snapshot = JsonSerializer.Deserialize<CadVisualizationSnapshot>(
                File.ReadAllText(snapshotPath),
                JsonOptions);
            if (snapshot is null
                || !snapshot.SourceSha256.Equals(
                    checksum,
                    StringComparison.OrdinalIgnoreCase)
                || !Path.GetFullPath(snapshot.SourceDwgPath).Equals(
                    sourcePath,
                    StringComparison.OrdinalIgnoreCase)
                || !File.Exists(snapshot.ArtifactPath))
            {
                return null;
            }

            return snapshot with { LoadedFromCache = true };
        }
        catch (Exception exception) when (
            exception is JsonException
                or IOException
                or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task<CadVisualizationSnapshot> GenerateAsync(
        Guid projectId,
        string sourceDwgPath,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "O projeto da visualização é obrigatório.",
                nameof(projectId));
        }

        var sourcePath = ValidateSourcePath(sourceDwgPath);
        var sourceChecksum = await FileChecksum
            .ComputeSha256Async(sourcePath, cancellationToken)
            .ConfigureAwait(false);
        var cached = TryLoadCached(projectId, sourcePath);
        if (cached is not null)
        {
            return cached;
        }

        var engine = GetEngineStatus();
        if (!engine.IsAvailable)
        {
            throw new InvalidOperationException(engine.Message);
        }

        var projectRoot = Path.Combine(_storageRoot, projectId.ToString("N"));
        var runsRoot = Path.Combine(projectRoot, "runs");
        Directory.CreateDirectory(runsRoot);
        var runId = Guid.NewGuid();
        var runRoot = Path.Combine(runsRoot, runId.ToString("N"));
        Directory.CreateDirectory(runRoot);
        var copyPath = Path.Combine(runRoot, "source-copy.dwg");
        var artifactPath = Path.Combine(runRoot, "geometry.aiv");

        try
        {
            await CopyNewAsync(sourcePath, copyPath, cancellationToken)
                .ConfigureAwait(false);
            await EnsureChecksumAsync(
                    copyPath,
                    sourceChecksum,
                    "A cópia técnica não corresponde ao DWG original.",
                    cancellationToken)
                .ConfigureAwait(false);
            await EnsureChecksumAsync(
                    sourcePath,
                    sourceChecksum,
                    "O DWG original mudou durante a preparação da visualização.",
                    cancellationToken)
                .ConfigureAwait(false);

            await _exporter.ExportAsync(
                    copyPath,
                    artifactPath,
                    runRoot,
                    cancellationToken)
                .ConfigureAwait(false);
            var geometry = _parser.Parse(artifactPath);
            await EnsureChecksumAsync(
                    sourcePath,
                    sourceChecksum,
                    "O DWG original mudou durante a exportação gráfica.",
                    cancellationToken)
                .ConfigureAwait(false);

            var cacheRoot = GetCacheRoot(projectId, sourceChecksum);
            var finalArtifactPath = Path.Combine(cacheRoot, "geometry.aiv");
            var snapshot = new CadVisualizationSnapshot(
                projectId,
                sourcePath,
                sourceChecksum,
                engine.EngineName,
                engine.Version,
                _timeProvider.GetUtcNow(),
                geometry.Bounds,
                geometry.Layers,
                geometry.Primitives,
                geometry.Coverage,
                finalArtifactPath,
                false);
            await File.WriteAllTextAsync(
                    Path.Combine(runRoot, "snapshot.json"),
                    JsonSerializer.Serialize(snapshot, JsonOptions),
                    cancellationToken)
                .ConfigureAwait(false);
            await File.WriteAllTextAsync(
                    Path.Combine(runRoot, "manifest.txt"),
                    BuildManifest(snapshot, runId),
                    cancellationToken)
                .ConfigureAwait(false);

            await PublishCacheAsync(
                    projectRoot,
                    runId,
                    runRoot,
                    cacheRoot,
                    cancellationToken)
                .ConfigureAwait(false);

            var published = TryLoadCached(projectId, sourcePath);
            return published is null
                ? snapshot with
                {
                    ArtifactPath = File.Exists(finalArtifactPath)
                        ? finalArtifactPath
                        : artifactPath
                }
                : published with { LoadedFromCache = false };
        }
        catch (Exception exception)
        {
            TryWriteFailure(runRoot, exception);
            throw;
        }
    }

    private string GetSnapshotPath(Guid projectId, string checksum) =>
        Path.Combine(GetCacheRoot(projectId, checksum), "snapshot.json");

    private string GetCacheRoot(Guid projectId, string checksum) =>
        Path.Combine(
            _storageRoot,
            projectId.ToString("N"),
            "cache",
            PipelineVersion,
            checksum);

    private static async Task PublishCacheAsync(
        string projectRoot,
        Guid runId,
        string runRoot,
        string cacheRoot,
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(cacheRoot)
            && File.Exists(Path.Combine(cacheRoot, "snapshot.json"))
            && File.Exists(Path.Combine(cacheRoot, "geometry.aiv")))
        {
            return;
        }

        var stagingRoot = Path.Combine(
            projectRoot,
            "cache-staging",
            runId.ToString("N"));
        Directory.CreateDirectory(stagingRoot);
        await CopyPublishedFileWithRetryAsync(
                Path.Combine(runRoot, "geometry.aiv"),
                Path.Combine(stagingRoot, "geometry.aiv"),
                cancellationToken)
            .ConfigureAwait(false);
        await CopyPublishedFileWithRetryAsync(
                Path.Combine(runRoot, "snapshot.json"),
                Path.Combine(stagingRoot, "snapshot.json"),
                cancellationToken)
            .ConfigureAwait(false);
        await CopyPublishedFileWithRetryAsync(
                Path.Combine(runRoot, "manifest.txt"),
                Path.Combine(stagingRoot, "manifest.txt"),
                cancellationToken)
            .ConfigureAwait(false);

        Directory.CreateDirectory(Path.GetDirectoryName(cacheRoot)!);
        if (!Directory.Exists(cacheRoot))
        {
            Directory.Move(stagingRoot, cacheRoot);
        }
    }

    private static async Task CopyPublishedFileWithRetryAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                await using var source = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    131_072,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using var destination = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    131_072,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(destination, cancellationToken)
                    .ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                await Task.Delay(
                        TimeSpan.FromMilliseconds(250),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static string ValidateSourcePath(string sourceDwgPath)
    {
        if (string.IsNullOrWhiteSpace(sourceDwgPath))
        {
            throw new ArgumentException(
                "O caminho do DWG é obrigatório.",
                nameof(sourceDwgPath));
        }

        var fullPath = Path.GetFullPath(sourceDwgPath);
        if (!Path.GetExtension(fullPath).Equals(
                ".dwg",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "A visualização CAD aceita somente arquivos DWG.");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "O DWG referenciado pelos dados semânticos não foi encontrado.",
                fullPath);
        }

        return fullPath;
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
            131_072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            131_072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, cancellationToken)
            .ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureChecksumAsync(
        string path,
        string expected,
        string message,
        CancellationToken cancellationToken)
    {
        var checksum = await FileChecksum
            .ComputeSha256Async(path, cancellationToken)
            .ConfigureAwait(false);
        if (!checksum.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(message);
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            131_072,
            FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string BuildManifest(
        CadVisualizationSnapshot snapshot,
        Guid runId) =>
        $"AUTOAIBUILDER CAD VISUALIZATION {PipelineVersion}{Environment.NewLine}"
        + $"RunId={runId:N}{Environment.NewLine}"
        + $"ProjectId={snapshot.ProjectId:N}{Environment.NewLine}"
        + $"Source={snapshot.SourceDwgPath}{Environment.NewLine}"
        + $"SourceSha256={snapshot.SourceSha256}{Environment.NewLine}"
        + $"Engine={snapshot.EngineName}{Environment.NewLine}"
        + $"EngineVersion={snapshot.EngineVersion}{Environment.NewLine}"
        + $"GeneratedAt={snapshot.GeneratedAt:O}{Environment.NewLine}"
        + $"Primitives={snapshot.Primitives.Count}{Environment.NewLine}"
        + $"Layers={snapshot.Layers.Count}{Environment.NewLine}"
        + $"Coverage={snapshot.Coverage.Percentage:0.##}%{Environment.NewLine}"
        + $"RemainingBlocks={snapshot.Coverage.RemainingBlockCount}{Environment.NewLine}"
        + "OriginalModified=False"
        + Environment.NewLine;

    private static void TryWriteFailure(string runRoot, Exception exception)
    {
        try
        {
            if (Directory.Exists(runRoot))
            {
                File.WriteAllText(
                    Path.Combine(runRoot, "failure.txt"),
                    $"{DateTimeOffset.UtcNow:O}{Environment.NewLine}"
                    + $"{exception.GetType().FullName}{Environment.NewLine}"
                    + exception.Message);
            }
        }
        catch
        {
            // A falha original permanece prioritária.
        }
    }
}
