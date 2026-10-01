using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoAIBuilder.Application.Automation.Supervised;

namespace AutoAIBuilder.Infrastructure.Automation.Supervised;

public sealed class SupervisedAutomationService(
    ISupervisedCadRunner runner,
    TimeProvider? timeProvider = null) : ISupervisedAutomationService
{
    private static readonly string[] RequiredScripts =
    [
        "mascara_previsualizar_v04.lsp",
        "mascara_camadas_v05.lsp",
        "mascara_limpeza_v06.lsp"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly TimeProvider _timeProvider =
        timeProvider ?? TimeProvider.System;

    public SupervisedAutomationRunResult? LoadLatest(
        Guid projectId,
        string outputRoot)
    {
        if (projectId == Guid.Empty
            || string.IsNullOrWhiteSpace(outputRoot)
            || !Path.IsPathFullyQualified(outputRoot))
        {
            return null;
        }

        var projectDirectory = Path.Combine(
            Path.GetFullPath(outputRoot),
            projectId.ToString("N"));
        if (!Directory.Exists(projectDirectory))
        {
            return null;
        }

        foreach (var runDirectory in Directory
                     .EnumerateDirectories(projectDirectory)
                     .OrderByDescending(
                         path => path,
                         StringComparer.OrdinalIgnoreCase))
        {
            var manifestPath = Path.Combine(
                runDirectory,
                "manifesto-11.6h.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(manifestPath, Encoding.UTF8);
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty(
                        "Result",
                        out var storedResult)
                    || !storedResult.TryGetProperty(
                        "ExecutionProfile",
                        out var storedProfile))
                {
                    continue;
                }

                var profile = storedProfile.GetString();
                if (!SupervisedAutomationProfiles.PreservationTotal.Equals(
                        profile,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var manifest = JsonSerializer.Deserialize<StoredManifest>(json);
                if (manifest?.Result.ProjectId == projectId
                    && File.Exists(manifest.Result.ResultDwgPath))
                {
                    return manifest.Result;
                }
            }
            catch (JsonException)
            {
                // Um manifesto inválido não impede a leitura de execuções anteriores.
            }
            catch (IOException)
            {
                // Uma execução ainda em gravação é ignorada nesta atualização.
            }
        }

        return null;
    }

    public SupervisedAutomationPreflight Inspect(
        SupervisedAutomationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var gates = new List<SupervisedAutomationGate>();
        var expectedPoints = request.Points.Count(point =>
            !string.IsNullOrWhiteSpace(point.Handle)
            && !string.IsNullOrWhiteSpace(point.SemanticLayer));
        var expectedComponents = request.Components.Count(component =>
            !string.IsNullOrWhiteSpace(component.Handle)
            && !string.IsNullOrWhiteSpace(component.SemanticLayer));
        AddGate(
            gates,
            "PREVIEW_APPROVED",
            "Aprovação do 11.6G",
            request.PreviewPlan.IsReadyForSupervisedExecution,
            request.PreviewPlan.IsReadyForSupervisedExecution
                ? $"{request.PreviewPlan.ApprovedCount} grupos aprovados; "
                  + "nenhuma pendência ou rejeição."
                : $"{request.PreviewPlan.PendingCount} pendente(s), "
                  + $"{request.PreviewPlan.RejectedCount} rejeitado(s).");
        var sourceExists = IsDwg(request.SourceDwgPath);
        var sourceHash = sourceExists
            ? ComputeSha256(request.SourceDwgPath)
            : string.Empty;
        AddGate(
            gates,
            "SOURCE_INTEGRITY",
            "Integridade do DWG original",
            sourceExists,
            sourceExists
                ? $"SHA-256 atual: {sourceHash}"
                : "O DWG original não foi encontrado.");

        var historicalMaskExists = IsDwg(request.HistoricalMaskPath);
        var historicalMaskHash = historicalMaskExists
            ? ComputeSha256(request.HistoricalMaskPath)
            : string.Empty;
        AddGate(
            gates,
            "HISTORICAL_MASK",
            "Máscara histórica validada",
            historicalMaskExists,
            historicalMaskExists
                ? $"{request.HistoricalMaskPath} | SHA-256 {historicalMaskHash}"
                : "Selecione uma máscara histórica DWG válida.");
        var distinctProtectedFiles = sourceExists
            && historicalMaskExists
            && !Path.GetFullPath(request.SourceDwgPath).Equals(
                Path.GetFullPath(request.HistoricalMaskPath),
                StringComparison.OrdinalIgnoreCase)
            && !sourceHash.Equals(
                historicalMaskHash,
                StringComparison.OrdinalIgnoreCase);
        AddGate(
            gates,
            "SOURCE_REFERENCE_DISTINCT",
            "Original diferente da referência",
            distinctProtectedFiles,
            distinctProtectedFiles
                ? "O DWG de entrada e a máscara V06 são arquivos distintos."
                : "O DWG de entrada não pode ser a própria máscara histórica "
                  + "nem uma cópia binariamente idêntica.");

        var historicalRows = File.Exists(request.HistoricalPointsCsvPath)
            ? ReadCsv(request.HistoricalPointsCsvPath)
            : [];
        var historicalPoints = historicalRows.Count(row =>
            row.ContainsKey("HANDLE_PONTO")
            && row.ContainsKey("LAYER_SEMANTICA"));
        AddGate(
            gates,
            "HISTORICAL_REPORT",
            "Relatório de pontos da referência",
            historicalPoints > 0 && historicalPoints == expectedPoints,
            historicalPoints > 0
                ? $"{historicalPoints} pontos de referência; "
                  + $"{expectedPoints} esperados no plano."
                : "O CSV histórico v07 não foi encontrado ou é incompatível.");

        var legendRows = File.Exists(request.LegendObjectsCsvPath)
            ? ReadCsv(request.LegendObjectsCsvPath)
            : [];
        var legendHandles = legendRows
            .Select(row => Get(row, "HANDLE"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        AddGate(
            gates,
            "LEGEND_SELECTION",
            "Seleção histórica da legenda",
            legendHandles > 0,
            legendHandles > 0
                ? $"{legendHandles} objetos identificados por handle."
                : "O catálogo de objetos da legenda não foi localizado.");

        AddGate(
            gates,
            "PRESERVATION_MODE",
            "Modo de preservação total",
            true,
            "O adaptador 11.6H.1 não contém operações de exclusão; "
            + "somente os handles semânticos aprovados podem mudar de layer.");

        var missingScripts = RequiredScripts
            .Where(name => !File.Exists(
                Path.Combine(request.AutoLispDirectory ?? string.Empty, name)))
            .ToArray();
        AddGate(
            gates,
            "AUTOLISP_SOURCES",
            "Rotinas AutoLISP auditadas",
            missingScripts.Length == 0,
            missingScripts.Length == 0
                ? "V04, V05 e V06 encontradas; os arquivos originais serão preservados."
                : $"Ausentes: {string.Join(", ", missingScripts)}.");

        var engine = runner.GetStatus();
        AddGate(
            gates,
            "AUTOCAD_ENGINE",
            "Motor oficial Autodesk",
            engine.IsAvailable,
            engine.IsAvailable
                ? $"{engine.Name} {engine.Version} | {engine.ExecutablePath}"
                : engine.Message);

        var outputValid = IsSafeOutputRoot(
            request.OutputRoot,
            request.SourceDwgPath);
        AddGate(
            gates,
            "ISOLATED_OUTPUT",
            "Destino técnico isolado",
            outputValid,
            outputValid
                ? Path.GetFullPath(request.OutputRoot)
                : "Escolha uma pasta de saída diferente da pasta do DWG original.");

        AddGate(
            gates,
            "SEMANTIC_BASE",
            "Base semântica revisada",
            expectedPoints > 0,
            expectedPoints > 0
                ? $"{expectedPoints} pontos e {expectedComponents} componentes "
                  + "com handle e camada esperada."
                : "A base semântica não contém pontos executáveis.");

        return new SupervisedAutomationPreflight(
            request.ProjectId,
            request.PreviewPlan.Id,
            request.SourceDwgPath,
            sourceHash,
            request.HistoricalMaskPath,
            historicalMaskHash,
            request.HistoricalPointsCsvPath,
            request.LegendObjectsCsvPath,
            request.AutoLispDirectory,
            request.OutputRoot,
            expectedPoints,
            expectedComponents,
            historicalPoints,
            legendHandles,
            gates,
            gates.All(gate => gate.Status != SupervisedAutomationGateStatus.Blocked),
            "A rotina escreverá somente na cópia técnica e não executará "
            + "exclusões. Todos os handles do Model Space serão comparados "
            + "antes e depois; original e referência também serão "
            + "revalidados por SHA-256.");
    }

    public async Task<SupervisedAutomationRunResult> ExecuteAsync(
        SupervisedAutomationRequest request,
        CancellationToken cancellationToken = default)
    {
        var preflight = Inspect(request);
        if (!preflight.CanExecute)
        {
            var blockers = preflight.Gates
                .Where(gate => gate.Status == SupervisedAutomationGateStatus.Blocked)
                .Select(gate => gate.Name);
            throw new InvalidOperationException(
                "Execução supervisionada bloqueada: "
                + string.Join("; ", blockers) + ".");
        }

        var startedAt = _timeProvider.GetUtcNow();
        var runId = Guid.NewGuid();
        var runDirectory = Path.Combine(
            Path.GetFullPath(request.OutputRoot),
            request.ProjectId.ToString("N"),
            $"{startedAt:yyyyMMdd-HHmmss}-{runId:N}");
        var inputDirectory = Path.Combine(runDirectory, "entrada");
        var resultDirectory = Path.Combine(runDirectory, "resultado");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(resultDirectory);

        var sourceSnapshotPath = Path.Combine(
            inputDirectory,
            "ORIGINAL_VERIFICADO.dwg");
        var resultDwgPath = Path.Combine(
            resultDirectory,
            $"AUTOAIBUILDER_PONTOS_PRESERVADOS_{startedAt:yyyyMMdd_HHmmss}.dwg");
        var manifestPath = Path.Combine(runDirectory, "manifesto-11.6h.json");
        try
        {
            await CopyNewAsync(
                    preflight.SourceDwgPath,
                    sourceSnapshotPath,
                    cancellationToken)
                .ConfigureAwait(false);
            await CopyNewAsync(
                    sourceSnapshotPath,
                    resultDwgPath,
                    cancellationToken)
                .ConfigureAwait(false);
            var technicalCopyHash = await FileChecksum.ComputeSha256Async(
                    resultDwgPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!technicalCopyHash.Equals(
                    preflight.SourceSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "A cópia técnica inicial não corresponde ao DWG original.");
            }

            var assignments = request.Points
                .Where(point =>
                    !string.IsNullOrWhiteSpace(point.Handle)
                    && !string.IsNullOrWhiteSpace(point.SemanticLayer))
                .Select(point => new SupervisedCadAssignment(
                    point.Handle,
                    point.SemanticLayer,
                    "PONTO"))
                .Concat(
                    request.Components
                        .Where(component =>
                            !string.IsNullOrWhiteSpace(component.Handle)
                            && !string.IsNullOrWhiteSpace(
                                component.SemanticLayer))
                        .Select(component => new SupervisedCadAssignment(
                            component.Handle,
                            component.SemanticLayer,
                            "COMPONENTE")))
                .ToArray();
            var cad = await runner.RunAsync(
                    new SupervisedCadRunnerRequest(
                        resultDwgPath,
                        request.AutoLispDirectory,
                        runDirectory,
                        assignments),
                    cancellationToken)
                .ConfigureAwait(false);
            var resultRows = ReadCsv(cad.ReportPath);
            var actual = resultRows
                .Where(row => Get(row, "KIND").Equals(
                    "PONTO",
                    StringComparison.OrdinalIgnoreCase))
                .Where(row => !string.IsNullOrWhiteSpace(Get(row, "HANDLE")))
                .GroupBy(
                    row => Get(row, "HANDLE"),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => Get(group.First(), "LAYER"),
                    StringComparer.OrdinalIgnoreCase);
            var actualComponents = resultRows
                .Where(row => Get(row, "KIND").Equals(
                    "COMPONENTE",
                    StringComparison.OrdinalIgnoreCase))
                .Where(row => !string.IsNullOrWhiteSpace(Get(row, "HANDLE")))
                .GroupBy(
                    row => Get(row, "HANDLE"),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => Get(group.First(), "LAYER"),
                    StringComparer.OrdinalIgnoreCase);
            var historical = ReadCsv(request.HistoricalPointsCsvPath)
                .Where(row => !string.IsNullOrWhiteSpace(
                    Get(row, "HANDLE_PONTO")))
                .GroupBy(
                    row => Get(row, "HANDLE_PONTO"),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => Get(group.First(), "LAYER_SEMANTICA"),
                    StringComparer.OrdinalIgnoreCase);
            var comparisons = request.Points
                .Where(point => !string.IsNullOrWhiteSpace(point.Handle))
                .Select(point =>
                {
                    actual.TryGetValue(point.Handle, out var actualLayer);
                    return new SupervisedPointComparison(
                        point.Handle,
                        point.SemanticLayer,
                        actualLayer ?? string.Empty,
                        actualLayer is not null,
                        actualLayer?.Equals(
                            point.SemanticLayer,
                            StringComparison.OrdinalIgnoreCase) == true);
                })
                .ToArray();
            var correct = comparisons.Count(item => item.LayerMatches);
            var missing = comparisons.Count(item => !item.Found);
            var wrongLayer = comparisons.Count(item =>
                item.Found && !item.LayerMatches);
            var historicalMatches = actual.Count(pair =>
                historical.TryGetValue(pair.Key, out var layer)
                && layer.Equals(pair.Value, StringComparison.OrdinalIgnoreCase));
            var correctComponents = request.Components.Count(component =>
                actualComponents.TryGetValue(component.Handle, out var layer)
                && layer.Equals(
                    component.SemanticLayer,
                    StringComparison.OrdinalIgnoreCase));
            var preservation = ReadPreservation(
                cad.PreservationReportPath);
            var metrics = new SupervisedAutomationMetrics(
                comparisons.Length,
                comparisons.Length - missing,
                correct,
                missing,
                wrongLayer,
                comparisons.Length == 0
                    ? 0
                    : correct * 100d / comparisons.Length,
                preflight.ExpectedComponentCount,
                correctComponents,
                preservation.EntitiesBefore,
                preservation.EntitiesAfter,
                preservation.MissingHandles,
                preservation.ProtectedBlocksBefore,
                preservation.ProtectedBlocksAfter,
                historical.Count,
                historicalMatches);
            var acceptancePassed =
                correct == comparisons.Length
                && missing == 0
                && wrongLayer == 0
                && correctComponents == preflight.ExpectedComponentCount
                && preservation.EntitiesBefore > 0
                && preservation.EntitiesAfter == preservation.EntitiesBefore
                && preservation.MissingHandles == 0
                && preservation.ProtectedBlocksAfter
                   == preservation.ProtectedBlocksBefore
                && historicalMatches == historical.Count;
            var sourceHashAfter = await FileChecksum.ComputeSha256Async(
                    preflight.SourceDwgPath,
                    cancellationToken)
                .ConfigureAwait(false);
            var historicalHashAfter = await FileChecksum.ComputeSha256Async(
                    preflight.HistoricalMaskPath,
                    cancellationToken)
                .ConfigureAwait(false);
            var resultHash = await FileChecksum.ComputeSha256Async(
                    resultDwgPath,
                    cancellationToken)
                .ConfigureAwait(false);
            var completedAt = _timeProvider.GetUtcNow();
            var sourceIntegrity = sourceHashAfter.Equals(
                preflight.SourceSha256,
                StringComparison.OrdinalIgnoreCase);
            var historicalIntegrity = historicalHashAfter.Equals(
                preflight.HistoricalMaskSha256,
                StringComparison.OrdinalIgnoreCase);
            var result = new SupervisedAutomationRunResult(
                runId,
                request.ProjectId,
                request.PreviewPlan.Id,
                SupervisedAutomationProfiles.PreservationTotal,
                startedAt,
                completedAt,
                runDirectory,
                resultDwgPath,
                manifestPath,
                cad.LogPath,
                preflight.SourceSha256,
                sourceHashAfter,
                technicalCopyHash,
                resultHash,
                sourceIntegrity,
                historicalIntegrity,
                acceptancePassed,
                metrics,
                comparisons,
                acceptancePassed
                    ? $"{correct}/{comparisons.Length} pontos corretos "
                      + $"({metrics.AccuracyPercentage:0.##}%); "
                      + $"{correctComponents}/{preflight.ExpectedComponentCount} "
                      + "componentes corretos; "
                      + $"{preservation.EntitiesAfter}/"
                      + $"{preservation.EntitiesBefore} entidades preservadas; "
                      + "zero handles ausentes; "
                      + $"{historicalMatches}/{historical.Count} correspondências "
                      + "com a máscara histórica."
                    : "RESULTADO REPROVADO: "
                      + $"{correct}/{comparisons.Length} pontos corretos, "
                      + $"{missing} ausentes, {wrongLayer} em layer divergente "
                      + $"e {correctComponents}/{preflight.ExpectedComponentCount} "
                      + "componentes corretos; "
                      + $"{preservation.MissingHandles} handles arquitetônicos "
                      + "ausentes.");
            await File.WriteAllTextAsync(
                    manifestPath,
                    JsonSerializer.Serialize(
                        new
                        {
                            Milestone = "11.6H.1",
                            Policy = "PRESERVATION_TOTAL",
                            Result = result,
                            Preflight = preflight,
                            OriginalModified = !sourceIntegrity,
                            HistoricalReferenceModified = !historicalIntegrity
                        },
                        JsonOptions),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!sourceIntegrity || !historicalIntegrity)
            {
                throw new InvalidDataException(
                    "Uma fonte protegida mudou durante a execução. "
                    + "O resultado foi isolado para auditoria.");
            }

            return result;
        }
        catch (Exception exception)
        {
            await TryWriteFailureAsync(
                    runDirectory,
                    exception,
                    CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
    }

    private static PreservationSnapshot ReadPreservation(string path)
    {
        var row = ReadCsv(path).FirstOrDefault();
        return row is null
            ? new PreservationSnapshot(0, 0, int.MaxValue, 0, 0)
            : new PreservationSnapshot(
                ParseInt(row, "ENTIDADES_ANTES"),
                ParseInt(row, "ENTIDADES_DEPOIS"),
                ParseInt(row, "HANDLES_AUSENTES"),
                ParseInt(row, "CINZA_PONTOS_ANTES"),
                ParseInt(row, "CINZA_PONTOS_DEPOIS"));
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> row,
        string key) =>
        int.TryParse(Get(row, key), out var value) ? value : 0;

    private static void AddGate(
        ICollection<SupervisedAutomationGate> gates,
        string code,
        string name,
        bool passed,
        string evidence) =>
        gates.Add(
            new SupervisedAutomationGate(
                code,
                name,
                passed
                    ? SupervisedAutomationGateStatus.Passed
                    : SupervisedAutomationGateStatus.Blocked,
                evidence));

    private static bool IsDwg(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && File.Exists(path)
        && Path.GetExtension(path).Equals(
            ".dwg",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeOutputRoot(string? outputRoot, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(outputRoot)
            || !Path.IsPathFullyQualified(outputRoot))
        {
            return false;
        }

        var output = Path.GetFullPath(outputRoot)
            .TrimEnd(Path.DirectorySeparatorChar);
        var sourceDirectory = Path.GetDirectoryName(
            Path.GetFullPath(sourcePath))?
            .TrimEnd(Path.DirectorySeparatorChar);
        return !string.IsNullOrWhiteSpace(sourceDirectory)
               && !output.Equals(
                   sourceDirectory,
                   StringComparison.OrdinalIgnoreCase);
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

    private static List<Dictionary<string, string>> ReadCsv(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var reader = new StreamReader(
            path,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return [];
        }

        var headers = ParseRow(headerLine);
        var result = new List<Dictionary<string, string>>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseRow(line);
            var row = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Count; index++)
            {
                row[headers[index]] = index < values.Count
                    ? values[index]
                    : string.Empty;
            }

            result.Add(row);
        }

        return result;
    }

    private static List<string> ParseRow(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ';' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    private static string Get(
        IReadOnlyDictionary<string, string> row,
        string key) =>
        row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static async Task TryWriteFailureAsync(
        string runDirectory,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            if (Directory.Exists(runDirectory))
            {
                await File.WriteAllTextAsync(
                        Path.Combine(runDirectory, "falha-11.6h.txt"),
                        $"{DateTimeOffset.UtcNow:O}{Environment.NewLine}"
                        + $"{exception.GetType().FullName}{Environment.NewLine}"
                        + exception.Message,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            // A falha original permanece prioritária.
        }
    }

    private sealed record StoredManifest(
        SupervisedAutomationRunResult Result);

    private sealed record PreservationSnapshot(
        int EntitiesBefore,
        int EntitiesAfter,
        int MissingHandles,
        int ProtectedBlocksBefore,
        int ProtectedBlocksAfter);
}
