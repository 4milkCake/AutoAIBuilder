using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoAIBuilder.Application.CadVisualization;
using AutoAIBuilder.Application.Recognition;
using AutoAIBuilder.Application.Semantics;
using AutoAIBuilder.Domain.Recognition;
using AutoAIBuilder.Domain.Semantics;
using AutoAIBuilder.Infrastructure.Automation;
using AutoAIBuilder.Infrastructure.Persistence;

namespace AutoAIBuilder.Infrastructure.Recognition;

public sealed class CadRecognitionService : ICadRecognitionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ICadEntityInventoryExporter _exporter;
    private readonly CadEntityInventoryParser _parser;
    private readonly CadLegendInterpreter _legendInterpreter;
    private readonly string _storageRoot;
    private readonly TimeProvider _timeProvider;

    public CadRecognitionService(
        ICadEntityInventoryExporter exporter,
        CadEntityInventoryParser? parser = null,
        CadLegendInterpreter? legendInterpreter = null,
        string? storageRoot = null,
        TimeProvider? timeProvider = null)
    {
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _parser = parser ?? new CadEntityInventoryParser();
        _legendInterpreter = legendInterpreter ?? new CadLegendInterpreter();
        _storageRoot = storageRoot ?? AppStoragePaths.RecognitionDirectory;
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

    public RecognitionSession? LoadLatest(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            return null;
        }

        var projectRoot = GetProjectRoot(projectId);
        if (!Directory.Exists(projectRoot))
        {
            return null;
        }

        foreach (var path in Directory
                     .EnumerateFiles(
                         projectRoot,
                         "session.json",
                         SearchOption.AllDirectories)
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                var session = JsonSerializer.Deserialize<RecognitionSession>(
                    File.ReadAllText(path),
                    JsonOptions);
                if (session?.ProjectId == projectId)
                {
                    return session;
                }
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or JsonException)
            {
                // Um artefato incompleto não invalida as execuções anteriores.
            }
        }

        return null;
    }

    public async Task<RecognitionSession> AnalyzeAsync(
        Guid projectId,
        string sourceDwgPath,
        SemanticWorkspaceSnapshot reference,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "O projeto do reconhecimento é obrigatório.",
                nameof(projectId));
        }

        var sourcePath = ValidateSource(sourceDwgPath);
        var engine = GetEngineStatus();
        if (!engine.IsAvailable)
        {
            throw new InvalidOperationException(engine.Message);
        }

        var sourceHash = await FileChecksum
            .ComputeSha256Async(sourcePath, cancellationToken)
            .ConfigureAwait(false);
        var sessionId = Guid.NewGuid();
        var now = _timeProvider.GetUtcNow();
        var runDirectory = Path.Combine(
            GetProjectRoot(projectId),
            $"{now:yyyyMMdd-HHmmss}-{sessionId:N}");
        Directory.CreateDirectory(runDirectory);
        var copyPath = Path.Combine(runDirectory, "entrada-verificada.dwg");
        var inventoryPath = Path.Combine(runDirectory, "inventario.aar");
        var sessionPath = Path.Combine(runDirectory, "session.json");

        await CopyNewAsync(sourcePath, copyPath, cancellationToken)
            .ConfigureAwait(false);
        await EnsureHashAsync(
                copyPath,
                sourceHash,
                "A cópia técnica não corresponde ao DWG selecionado.",
                cancellationToken)
            .ConfigureAwait(false);
        await _exporter.ExportAsync(
                copyPath,
                inventoryPath,
                runDirectory,
                cancellationToken)
            .ConfigureAwait(false);
        var inventory = CadExpandedGeometryAnchorResolver.Resolve(
            _parser.Parse(inventoryPath));
        var legendAnalysis = _legendInterpreter.Analyze(inventory);
        await EnsureHashAsync(
                sourcePath,
                sourceHash,
                "O DWG original mudou durante a leitura.",
                cancellationToken)
            .ConfigureAwait(false);

        var calibration = ResolveCalibration(
            projectId,
            sourceHash,
            inventory,
            reference.Points,
            now);
        var candidates = Detect(
            inventory,
            reference,
            calibration.BlockPrecisionScores,
            legendAnalysis,
            now);
        var profileSource = reference.Dataset is null
            ? "Regras gerais sem base histórica carregada"
            : $"Base {reference.Dataset.DrawingFileName} "
              + $"({reference.Points.Count} pontos validados); "
              + (calibration.MatchedReferenceHandles > 0
                  ? $"{calibration.MatchedReferenceHandles} handles usados "
                    + "na calibração de precisão"
                  : "confiança conservadora até a primeira calibração");
        profileSource += legendAnalysis.IsDetected
            ? $"; legenda local com {legendAnalysis.Entries.Count} "
              + "par(es) símbolo–descrição"
            : "; legenda local ainda não detectada";
        var session = new RecognitionSession(
            sessionId,
            projectId,
            sourcePath,
            Path.GetFileName(sourcePath),
            sourceHash,
            engine.EngineName,
            engine.Version,
            profileSource,
            inventory.EntityCount,
            inventory.InsertCount,
            true,
            now,
            now,
            runDirectory,
            sessionPath,
            candidates,
            legendAnalysis,
            inventory.Entities.Count(entity =>
                entity.ObjectType.Equals(
                    "INSERT",
                    StringComparison.OrdinalIgnoreCase)
                && entity.ExpansionDepth > 0));
        await File.WriteAllTextAsync(
                sessionPath,
                JsonSerializer.Serialize(session, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                Path.Combine(runDirectory, "catalogo-legenda-11.6i.1c.json"),
                JsonSerializer.Serialize(legendAnalysis, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                Path.Combine(runDirectory, "manifesto-11.6i.txt"),
                BuildManifest(session),
                cancellationToken)
            .ConfigureAwait(false);
        return session;
    }

    public RecognitionSession Review(
        Guid projectId,
        Guid sessionId,
        Guid candidateId,
        RecognitionCandidateStatus status,
        RecognitionCorrectionRequest? correction = null,
        string? note = null)
    {
        if (status == RecognitionCandidateStatus.Pending)
        {
            throw new ArgumentException(
                "A revisão deve aprovar, corrigir ou rejeitar o candidato.",
                nameof(status));
        }

        if (status == RecognitionCandidateStatus.Corrected
            && correction is null)
        {
            throw new ArgumentException(
                "Informe a classificação corrigida.",
                nameof(correction));
        }

        var session = FindSession(projectId, sessionId);
        var candidate = session.Candidates.SingleOrDefault(
            item => item.Id == candidateId)
            ?? throw new KeyNotFoundException(
                "O candidato selecionado não pertence a esta análise.");
        var now = _timeProvider.GetUtcNow();
        var revised = correction is null
            ? candidate with
            {
                Status = status,
                ReviewNote = NormalizeOptional(note),
                ReviewedAt = now
            }
            : candidate with
            {
                ProposedDiscipline = correction.Discipline,
                ProposedCode = Required(
                    correction.SemanticCode,
                    "código semântico"),
                ProposedDescription = Required(
                    correction.Description,
                    "descrição"),
                ProposedLayer = Required(
                    correction.SemanticLayer,
                    "layer semântica"),
                ProposedHeight = correction.Height.Trim(),
                Confidence = SemanticConfidence.High,
                ConfidenceScore = 100,
                DetectionReason = "Classificação corrigida e confirmada pelo usuário.",
                Status = RecognitionCandidateStatus.Corrected,
                ReviewNote = NormalizeOptional(correction.Note),
                ReviewedAt = now
            };
        var candidates = session.Candidates
            .Select(item => item.Id == candidateId ? revised : item)
            .ToArray();
        var updated = session with
        {
            UpdatedAt = now,
            Candidates = candidates
        };
        File.WriteAllText(
            session.ManifestPath,
            JsonSerializer.Serialize(updated, JsonOptions));
        File.AppendAllText(
            Path.Combine(session.RunDirectory, "auditoria-revisao.jsonl"),
            JsonSerializer.Serialize(
                new
                {
                    OccurredAt = now,
                    CandidateId = candidateId,
                    PreviousStatus = candidate.Status,
                    CurrentStatus = revised.Status,
                    revised.ProposedCode,
                    revised.ProposedLayer,
                    revised.ReviewNote
                })
            + Environment.NewLine);
        return updated;
    }

    private RecognitionSession FindSession(Guid projectId, Guid sessionId)
    {
        var root = GetProjectRoot(projectId);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                "Nenhuma análise 11.6I foi encontrada para o projeto.");
        }

        foreach (var path in Directory.EnumerateFiles(
                     root,
                     "session.json",
                     SearchOption.AllDirectories))
        {
            var session = JsonSerializer.Deserialize<RecognitionSession>(
                File.ReadAllText(path),
                JsonOptions);
            if (session?.ProjectId == projectId && session.Id == sessionId)
            {
                return session;
            }
        }

        throw new FileNotFoundException(
            "A sessão de reconhecimento não foi encontrada.");
    }

    private static IReadOnlyList<RecognitionCandidate> Detect(
        CadEntityInventory inventory,
        SemanticWorkspaceSnapshot reference,
        IReadOnlyDictionary<string, int> blockPrecisionScores,
        RecognitionLegendAnalysis legendAnalysis,
        DateTimeOffset now)
    {
        var blockProfiles = BuildProfiles(
            reference.Points,
            point => point.BlockName);
        var layerProfiles = BuildProfiles(
            reference.Points,
            point => point.SemanticLayer);
        var legendSymbolHandles = legendAnalysis.Entries
            .Select(entry => entry.SymbolHandle)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var legendProfiles = legendAnalysis.Entries
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.GeometrySignature)
                || !string.IsNullOrWhiteSpace(entry.BlockName))
            .GroupBy(entry => entry.IsLooseGeometry
                ? GeometryOnlyLegendKey(entry.GeometrySignature)
                : LegendKey(
                    entry.GeometrySignature,
                    entry.BlockName))
            .ToDictionary(
                group => group.Key,
                group => new LegendBlockProfile(
                    group
                        .Select(entry => entry.Description)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    group.ToArray()),
                StringComparer.Ordinal);
        var candidates = new List<RecognitionCandidate>();
        foreach (var entity in inventory.Entities)
        {
            var isInsert = entity.ObjectType.Equals(
                "INSERT",
                StringComparison.OrdinalIgnoreCase);
            var isSymbolText = IsSymbolText(entity);
            if (!isInsert && !isSymbolText)
            {
                continue;
            }

            if (legendSymbolHandles.Contains(entity.Handle))
            {
                continue;
            }

            var blockKey = Normalize(entity.BlockName);
            var entityGeometrySignature =
                ResolveRecognitionGeometrySignature(entity);
            var legendKey = LegendKey(
                entityGeometrySignature,
                entity.BlockName);
            var layerKey = Normalize(entity.Layer);
            RecognitionProfile? profile = null;
            var score = 0;
            var reason = string.Empty;
            var legendMatched = false;
            var matchedByGeometryTemplate = false;
            var legendDescription = string.Empty;
            var legendEvidence = string.Empty;
            LegendBlockProfile? legendBlock = null;
            if (legendKey.Length > 0)
            {
                legendProfiles.TryGetValue(legendKey, out legendBlock);
            }

            if (legendBlock is null
                && !string.IsNullOrWhiteSpace(entityGeometrySignature))
            {
                legendProfiles.TryGetValue(
                    GeometryOnlyLegendKey(entityGeometrySignature),
                    out legendBlock);
                matchedByGeometryTemplate = legendBlock is not null;
            }

            if (legendBlock is not null)
            {
                legendMatched = true;
                matchedByGeometryTemplate |= legendBlock.Entries.Any(
                    entry => entry.IsLooseGeometry);
                legendEvidence =
                    $"{legendBlock.Entries.Count} exemplar(es) com a mesma "
                    + (string.IsNullOrWhiteSpace(entityGeometrySignature)
                        ? "identidade de bloco"
                        : "assinatura estrutural")
                    + " pareado(s) dentro da legenda.";
                if (legendBlock.Descriptions.Count == 1)
                {
                    legendDescription = legendBlock.Descriptions[0];
                    profile = FindKeywordProfile(
                        legendDescription,
                        reference.Points);
                    if (profile is not null)
                    {
                        score = matchedByGeometryTemplate ? 78 : 82;
                        reason =
                            "Símbolo reconhecido pela legenda do próprio DWG "
                            + $"como \"{legendDescription}\" e associado à "
                            + "base semântica validada; confirmação visual "
                            + "obrigatória nesta primeira leitura. "
                            + HierarchyReason(entity);
                    }
                    else
                    {
                        score = matchedByGeometryTemplate ? 70 : 74;
                        reason =
                            "Símbolo reconhecido pela legenda do próprio DWG "
                            + $"como \"{legendDescription}\"; código semântico "
                            + "ainda requer confirmação humana. "
                            + HierarchyReason(entity);
                    }
                }
                else
                {
                    legendDescription = string.Join(
                        " / ",
                        legendBlock.Descriptions);
                    score = 45;
                    reason =
                        "O mesmo nome de bloco aparece com descrições diferentes "
                        + "na legenda; classificação mantida como ambígua. "
                        + HierarchyReason(entity);
                }
            }
            else if (blockKey.Length > 0
                && blockProfiles.TryGetValue(blockKey, out profile))
            {
                if (blockPrecisionScores.TryGetValue(
                        blockKey,
                        out var calibratedScore))
                {
                    score = calibratedScore;
                    reason =
                        $"Nome de bloco reconhecido e calibrado com {score}% "
                        + "de precisão na base histórica.";
                }
                else
                {
                    score = 78;
                    reason =
                        $"Nome de bloco reconhecido em {profile.EvidenceCount} "
                        + "exemplo(s), ainda sem amostras negativas calibradas.";
                }
            }
            else if (layerProfiles.TryGetValue(layerKey, out profile))
            {
                score = 92;
                reason = "Layer semântica já conhecida na base validada.";
            }
            else
            {
                profile = FindKeywordProfile(entity, reference.Points);
                if (profile is not null)
                {
                    score = 62;
                    reason =
                        "Correspondência por vocabulário técnico; revisão obrigatória.";
                }
            }

            if (entity.ExpansionDepth > 0
                && !legendMatched
                && profile is null)
            {
                continue;
            }

            if (isSymbolText && !legendMatched)
            {
                continue;
            }

            if (entity.ExpansionDepth == 0
                && entity.NestedInsertCount > 0
                && !legendMatched
                && profile is null)
            {
                continue;
            }

            candidates.Add(new RecognitionCandidate(
                Guid.NewGuid(),
                entity.Handle,
                entity.ObjectType,
                entity.Layer,
                entity.BlockName,
                entity.Text,
                entity.X,
                entity.Y,
                entity.Z,
                entity.CoordinateSystem,
                entity.AnchorSource,
                entity.InsertionX,
                entity.InsertionY,
                entity.InsertionZ,
                entity.RotationDegrees,
                entity.ScaleX,
                entity.ScaleY,
                entity.ScaleZ,
                profile?.Discipline,
                profile?.Code ?? string.Empty,
                profile?.Description
                ?? (legendMatched
                    ? legendDescription
                    : "Símbolo ainda não classificado"),
                profile?.Layer ?? string.Empty,
                profile?.Height ?? string.Empty,
                score >= 85
                    ? SemanticConfidence.High
                    : score >= 60
                        ? SemanticConfidence.Medium
                        : SemanticConfidence.Unknown,
                score,
                profile is null
                    ? "Bloco inventariado sem correspondência confiável. "
                      + SpatialReason(entity)
                    : reason + " " + SpatialReason(entity),
                RecognitionCandidateStatus.Pending,
                null,
                null,
                legendMatched,
                legendDescription,
                legendEvidence,
                entity.ExpansionDepth,
                entity.RootHandle,
                entity.StablePath,
                entityGeometrySignature,
                entity.PrimitiveCount,
                entity.NestedInsertCount,
                matchedByGeometryTemplate));
        }

        return candidates
            .OrderByDescending(candidate => candidate.ConfidenceScore)
            .ThenBy(candidate => candidate.BlockName)
            .ThenBy(candidate => candidate.Handle)
            .ToArray();
    }

    private RecognitionCalibration ResolveCalibration(
        Guid projectId,
        string sourceSha256,
        CadEntityInventory inventory,
        IReadOnlyList<SemanticPoint> referencePoints,
        DateTimeOffset now)
    {
        var referenceHandles = referencePoints
            .Select(point => point.Handle)
            .Where(handle => !string.IsNullOrWhiteSpace(handle))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchedHandles = inventory.Entities.Count(entity =>
            entity.ObjectType.Equals(
                "INSERT",
                StringComparison.OrdinalIgnoreCase)
            && referenceHandles.Contains(entity.Handle));
        var minimumOverlap = Math.Max(
            1,
            (int)Math.Ceiling(referenceHandles.Count * 0.5));
        var path = GetCalibrationPath(projectId);
        if (referenceHandles.Count > 0 && matchedHandles >= minimumOverlap)
        {
            var precision = inventory.Entities
                .Where(entity => entity.ObjectType.Equals(
                    "INSERT",
                    StringComparison.OrdinalIgnoreCase))
                .Where(entity => !string.IsNullOrWhiteSpace(entity.BlockName))
                .GroupBy(entity => Normalize(entity.BlockName))
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var total = group.Count();
                        var positives = group.Count(entity =>
                            referenceHandles.Contains(entity.Handle));
                        return (int)Math.Round(
                            positives * 100d / total,
                            MidpointRounding.AwayFromZero);
                    },
                    StringComparer.Ordinal);
            var calibration = new RecognitionCalibration(
                sourceSha256,
                matchedHandles,
                now,
                precision);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(calibration, JsonOptions));
            return calibration;
        }

        if (File.Exists(path))
        {
            try
            {
                return JsonSerializer.Deserialize<RecognitionCalibration>(
                           File.ReadAllText(path),
                           JsonOptions)
                       ?? RecognitionCalibration.Empty;
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or JsonException)
            {
                // Uma calibração inválida não impede a análise conservadora.
            }
        }

        return RecognitionCalibration.Empty;
    }

    private static Dictionary<string, RecognitionProfile> BuildProfiles(
        IReadOnlyList<SemanticPoint> points,
        Func<SemanticPoint, string> keySelector)
    {
        var profiles = new Dictionary<string, RecognitionProfile>(
            StringComparer.Ordinal);
        foreach (var group in points
                     .Where(point =>
                         !string.IsNullOrWhiteSpace(keySelector(point)))
                     .GroupBy(point => Normalize(keySelector(point))))
        {
            var codes = group
                .Select(point => point.SemanticCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (codes.Length != 1)
            {
                continue;
            }

            profiles[group.Key] = CreateProfile(group.ToArray());
        }

        return profiles;
    }

    private static RecognitionProfile? FindKeywordProfile(
        CadInventoryEntity entity,
        IReadOnlyList<SemanticPoint> points) =>
        FindKeywordProfile(
            $"{entity.BlockName} {entity.Layer} {entity.Text}",
            points);

    private static RecognitionProfile? FindKeywordProfile(
        string sourceText,
        IReadOnlyList<SemanticPoint> points)
    {
        var text = Normalize(sourceText);
        string[][] tokenSets =
        [
            ["AR", "CONDICIONADO"],
            ["TOMADA"],
            ["INTERRUPTOR"],
            ["RALO", "LINEAR"],
            ["RALO"],
            ["TORNEIRA"],
            ["DUCHA", "HIGIENICA"],
            ["BACIA", "SANITARIA"],
            ["LAVA", "LOUCAS"],
            ["MAQUINA", "LAVAR"],
            ["PURIFICADOR"],
            ["AGUA", "GELADEIRA"],
            ["ILUMINACAO"],
            ["LUMINARIA"],
            ["PONTO", "LUZ"]
        ];
        foreach (var tokens in tokenSets)
        {
            if (!tokens.All(text.Contains))
            {
                continue;
            }

            var matches = points.Where(point =>
            {
                var pointText = Normalize(
                    $"{point.SemanticCode} {point.Description} "
                    + $"{point.SemanticLayer}");
                return tokens.All(pointText.Contains);
            }).ToArray();
            if (matches.Length > 0)
            {
                return CreateProfile(matches);
            }
        }

        return null;
    }

    private static RecognitionProfile CreateProfile(
        IReadOnlyList<SemanticPoint> points)
    {
        var representative = points
            .GroupBy(point => point.SemanticCode)
            .OrderByDescending(group => group.Count())
            .First()
            .First();
        var heights = points
            .Select(point =>
                point.HeightCm?.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                ?? point.HeightSourceValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new RecognitionProfile(
            representative.Discipline,
            representative.SemanticCode,
            representative.Description,
            representative.SemanticLayer,
            heights.Length == 1 ? heights[0] : "A_CONFIRMAR_POR_CONTEXTO",
            points.Count);
    }

    private static string SpatialReason(CadInventoryEntity entity) =>
        entity.AnchorSource switch
        {
            "BOUNDS_CENTER_WCS" =>
                "Marcador espacial corrigido para o centro geométrico "
                + "porque o ponto-base do bloco estava fora de seus limites.",
            "INSERTION_WCS" =>
                "Marcador espacial baseado no ponto de inserção em WCS.",
            "ENTITY_POINT_WCS" =>
                "Coordenada convertida para WCS.",
            "EXPANDED_GEOMETRY_CENTER_WCS" =>
                "Marcador corrigido pelo centro da geometria expandida; "
                + "o ponto-base original e o handle raiz foram preservados.",
            _ =>
                "Coordenada proveniente de inventário legado."
        };

    private static string ValidateSource(string sourceDwgPath)
    {
        if (string.IsNullOrWhiteSpace(sourceDwgPath))
        {
            throw new ArgumentException(
                "Selecione um DWG para reconhecimento.",
                nameof(sourceDwgPath));
        }

        var fullPath = Path.GetFullPath(sourceDwgPath);
        if (!Path.GetExtension(fullPath).Equals(
                ".dwg",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "O reconhecimento 11.6I aceita somente arquivos DWG.");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "O DWG selecionado não foi encontrado.",
                fullPath);
        }

        return fullPath;
    }

    private static async Task CopyNewAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            source,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            131_072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            131_072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureHashAsync(
        string path,
        string expected,
        string message,
        CancellationToken cancellationToken)
    {
        var current = await FileChecksum
            .ComputeSha256Async(path, cancellationToken)
            .ConfigureAwait(false);
        if (!current.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(message);
        }
    }

    private string GetProjectRoot(Guid projectId) =>
        Path.Combine(_storageRoot, projectId.ToString("N"));

    private string GetCalibrationPath(Guid projectId) =>
        Path.Combine(GetProjectRoot(projectId), "recognition-profile.json");

    private static string BuildManifest(RecognitionSession session) =>
        "AUTOAIBUILDER RECOGNITION 11.6I" + Environment.NewLine
        + $"SessionId={session.Id:N}{Environment.NewLine}"
        + $"ProjectId={session.ProjectId:N}{Environment.NewLine}"
        + $"Source={session.SourceDwgPath}{Environment.NewLine}"
        + $"SourceSha256={session.SourceSha256}{Environment.NewLine}"
        + $"OriginalIntegrityConfirmed={session.OriginalIntegrityConfirmed}"
        + Environment.NewLine
        + $"InventoryEntities={session.InventoryEntityCount}{Environment.NewLine}"
        + $"InventoryInserts={session.InventoryInsertCount}{Environment.NewLine}"
        + $"InventoryExpandedInserts={session.InventoryExpandedInsertCount}"
        + Environment.NewLine
        + $"Candidates={session.Candidates.Count}{Environment.NewLine}"
        + $"LegendDetected={session.LegendAnalysis?.IsDetected == true}"
        + Environment.NewLine
        + $"LegendAnchors={session.LegendAnalysis?.AnchorCount ?? 0}"
        + Environment.NewLine
        + $"LegendPairs={session.LegendAnalysis?.Entries.Count ?? 0}"
        + Environment.NewLine
        + $"LegendLooseGeometryTemplates="
        + $"{session.LegendAnalysis?.Entries.Count(entry => entry.IsLooseGeometry) ?? 0}"
        + Environment.NewLine
        + $"LegendExemplarsExcluded="
        + $"{session.LegendAnalysis?.Entries.Count ?? 0}{Environment.NewLine}"
        + $"LegendMatchedCandidates="
        + $"{session.Candidates.Count(candidate => candidate.LegendMatched)}"
        + Environment.NewLine
        + $"ExpandedInserts={session.InventoryExpandedInsertCount}"
        + Environment.NewLine
        + $"NestedCandidates="
        + $"{session.Candidates.Count(candidate => candidate.ExpansionDepth > 0)}"
        + Environment.NewLine
        + $"GeometryMatchedCandidates="
        + $"{session.Candidates.Count(candidate => candidate.MatchedByGeometryTemplate)}"
        + Environment.NewLine
        + $"HighConfidence={session.Candidates.Count(candidate => candidate.ConfidenceScore >= 85)}"
        + Environment.NewLine
        + $"NeedsReview={session.Candidates.Count(candidate => candidate.ConfidenceScore < 85)}"
        + Environment.NewLine
        + $"CoordinateSystemWcs={session.Candidates.Count(candidate => candidate.CoordinateSystem == "WCS")}"
        + Environment.NewLine
        + $"CorrectedBlockAnchors={session.Candidates.Count(candidate => candidate.PositionSource == "BOUNDS_CENTER_WCS")}"
        + Environment.NewLine
        + $"ExpandedGeometryAnchors="
        + $"{session.Candidates.Count(candidate => candidate.PositionSource == "EXPANDED_GEOMETRY_CENTER_WCS")}"
        + Environment.NewLine
        + "AarHierarchyVersion=3" + Environment.NewLine
        + "MutationCommands=0" + Environment.NewLine;

    private static string Normalize(string value)
    {
        var decomposed = (value ?? string.Empty)
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character)
                == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToUpperInvariant(character)
                : ' ');
        }

        return string.Join(
            ' ',
            builder.ToString().Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries));
    }

    private static string LegendKey(
        string geometrySignature,
        string blockName)
    {
        if (!string.IsNullOrWhiteSpace(geometrySignature))
        {
            var normalizedGeometry =
                geometrySignature.Trim().ToUpperInvariant();
            var normalizedName = Normalize(blockName);
            return normalizedName.Length == 0
                ? $"G:{normalizedGeometry}"
                : $"G:{normalizedGeometry}|B:{normalizedName}";
        }

        var normalizedBlock = Normalize(blockName);
        return normalizedBlock.Length == 0
            ? string.Empty
            : $"B:{normalizedBlock}";
    }

    private static string GeometryOnlyLegendKey(string geometrySignature) =>
        string.IsNullOrWhiteSpace(geometrySignature)
            ? string.Empty
            : $"GONLY:{geometrySignature.Trim().ToUpperInvariant()}";

    private static bool IsSymbolText(CadInventoryEntity entity)
    {
        if (!entity.ObjectType.Equals(
                "TEXT",
                StringComparison.OrdinalIgnoreCase)
            && !entity.ObjectType.Equals(
                "MTEXT",
                StringComparison.OrdinalIgnoreCase)
            && !entity.ObjectType.Equals(
                "ATTRIB",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalized = Normalize(entity.Text);
        return normalized.Length is > 0 and <= 3;
    }

    private static string ResolveRecognitionGeometrySignature(
        CadInventoryEntity entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.GeometrySignature))
        {
            return entity.GeometrySignature;
        }

        return IsSymbolText(entity)
            ? $"GLYPH:{Normalize(entity.Text)}"
            : string.Empty;
    }

    private static string HierarchyReason(CadInventoryEntity entity) =>
        entity.ExpansionDepth > 0
            ? $"Símbolo interno localizado no nível {entity.ExpansionDepth}; "
              + $"raiz original {entity.RootHandle}; caminho "
              + $"{entity.StablePath}."
            : "Símbolo localizado diretamente no Model Space.";

    private static string Required(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Informe {label}.");
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record RecognitionProfile(
        SemanticDiscipline Discipline,
        string Code,
        string Description,
        string Layer,
        string Height,
        int EvidenceCount);

    private sealed record LegendBlockProfile(
        IReadOnlyList<string> Descriptions,
        IReadOnlyList<RecognitionLegendEntry> Entries);

    private sealed record RecognitionCalibration(
        string SourceSha256,
        int MatchedReferenceHandles,
        DateTimeOffset CalibratedAt,
        IReadOnlyDictionary<string, int> BlockPrecisionScores)
    {
        public static RecognitionCalibration Empty { get; } =
            new(
                string.Empty,
                0,
                DateTimeOffset.MinValue,
                new Dictionary<string, int>(StringComparer.Ordinal));
    }
}
