using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AutoAIBuilder.Application.Semantics;
using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Infrastructure.Semantics;

public sealed class SemanticCsvWorkspaceService(
    ISemanticDatasetRepository repository) : ISemanticWorkspaceService
{
    private const long MaximumFileBytes = 50L * 1024L * 1024L;
    private const int MaximumRowsPerFile = 200_000;

    public SemanticWorkspaceSnapshot GetForProject(Guid projectId) =>
        repository.GetForProject(projectId);

    public async Task<SemanticImportResult> ImportAsync(
        Guid projectId,
        IReadOnlyList<string> csvPaths,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "Selecione um projeto antes de importar os dados semânticos.",
                nameof(projectId));
        }

        if (csvPaths is null || csvPaths.Count == 0)
        {
            throw new ArgumentException(
                "Selecione ao menos o CSV de pontos semânticos.",
                nameof(csvPaths));
        }

        var normalizedPaths = csvPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedPaths.Length != csvPaths.Count)
        {
            throw new InvalidDataException(
                "A seleção contém caminhos vazios ou arquivos repetidos.");
        }

        var documents = new List<SemanticCsvDocument>();
        foreach (var path in normalizedPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            documents.Add(await ReadDocumentAsync(path, cancellationToken));
        }

        var pointsDocument = GetSingleDocument(
            documents,
            SemanticCsvKind.Points,
            required: true);
        var componentsDocument = GetSingleDocument(
            documents,
            SemanticCsvKind.Components,
            required: false);
        var directionsDocument = GetSingleDocument(
            documents,
            SemanticCsvKind.Directions,
            required: false);

        var datasetId = Guid.NewGuid();
        var points = ParsePoints(pointsDocument!, datasetId, cancellationToken);
        var components = componentsDocument is null
            ? []
            : ParseComponents(
                componentsDocument,
                datasetId,
                points,
                cancellationToken);
        var directions = directionsDocument is null
            ? []
            : ParseDirections(
                directionsDocument,
                datasetId,
                cancellationToken);

        var drawingName = Required(pointsDocument!.Rows[0], "ARQUIVO_DWG");
        var drawingPath = Required(pointsDocument.Rows[0], "CAMINHO_DWG");
        EnsureSingleDrawing(pointsDocument, drawingName, drawingPath);
        if (componentsDocument is not null)
        {
            EnsureSingleDrawing(componentsDocument, drawingName, drawingPath);
        }

        if (directionsDocument is not null)
        {
            EnsureSingleDrawing(directionsDocument, drawingName, drawingPath);
        }

        var electricalCount = points.Count(
            point => point.Discipline == SemanticDiscipline.Electrical);
        var hydraulicCount = points.Count(
            point => point.Discipline == SemanticDiscipline.Hydraulic);
        var layerCount = points
            .Select(point => point.SemanticLayer)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var reviewDirectionCount = directions.Count(
            direction => direction.NeedsReview);
        var builderValidatedCount = directions.Count(
            direction => direction.OffsetOrigin.Equals(
                "VALIDADO_NO_BUILDER",
                StringComparison.OrdinalIgnoreCase));
        var symmetryInferredCount = directions.Count(
            direction => direction.OffsetOrigin.Equals(
                "INFERIDO_POR_SIMETRIA",
                StringComparison.OrdinalIgnoreCase));
        var matchesHistoricalBaseline =
            points.Count == 272
            && electricalCount == 196
            && hydraulicCount == 76
            && components.Count == 100
            && layerCount == 35
            && directions.Count == 38
            && reviewDirectionCount == 2
            && builderValidatedCount == 15
            && symmetryInferredCount == 23;
        var now = DateTimeOffset.UtcNow;
        var sourceVersion = directions.FirstOrDefault()?.Version
            ?? InferVersion(pointsDocument.Path);
        var fingerprint = CalculateCombinedFingerprint(documents);
        var dataset = new SemanticDataset(
            datasetId,
            projectId,
            drawingName,
            drawingPath,
            sourceVersion,
            fingerprint,
            normalizedPaths,
            points.Count,
            electricalCount,
            hydraulicCount,
            components.Count,
            layerCount,
            directions.Count,
            reviewDirectionCount,
            builderValidatedCount,
            symmetryInferredCount,
            matchesHistoricalBaseline,
            now,
            now);
        var warnings = BuildWarnings(
            componentsDocument,
            directionsDocument,
            dataset);
        var replaced = repository.Replace(new SemanticImportPackage(
            dataset,
            points,
            components,
            directions));
        var persisted = repository.GetForProject(projectId).Dataset
            ?? throw new InvalidDataException(
                "A importação terminou sem um conjunto semântico persistido.");

        return new SemanticImportResult(persisted, replaced, warnings);
    }

    public void UpdatePointReview(
        Guid projectId,
        Guid pointId,
        SemanticReviewStatus status,
        string? note = null) =>
        repository.UpdatePointReview(
            projectId,
            pointId,
            status,
            note,
            DateTimeOffset.UtcNow);

    public SemanticPoint CorrectPoint(
        Guid projectId,
        Guid pointId,
        SemanticPointCorrectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return repository.CorrectPoint(
            projectId,
            pointId,
            request,
            DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<SemanticPoint> FindSimilarPoints(
        Guid projectId,
        Guid pointId) =>
        repository.FindSimilarPoints(projectId, pointId);

    public int ApplyPointCorrection(
        Guid projectId,
        Guid sourcePointId,
        IReadOnlyList<Guid> targetPointIds,
        string? note = null) =>
        repository.ApplyPointCorrection(
            projectId,
            sourcePointId,
            targetPointIds,
            note,
            DateTimeOffset.UtcNow);

    public SemanticDirectionDiagnostic ReviewDirection(
        Guid projectId,
        Guid directionId,
        SemanticDirectionReviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return repository.ReviewDirection(
            projectId,
            directionId,
            request,
            DateTimeOffset.UtcNow);
    }

    public bool UndoLatestRevision(
        Guid projectId,
        SemanticReviewEntityKind entityKind,
        Guid entityId) =>
        repository.UndoLatestRevision(
            projectId,
            entityKind,
            entityId,
            DateTimeOffset.UtcNow);

    private static async Task<SemanticCsvDocument> ReadDocumentAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Um dos arquivos CSV selecionados não existe.",
                path);
        }

        if (!Path.GetExtension(path).Equals(
                ".csv",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"O arquivo “{Path.GetFileName(path)}” não possui extensão CSV.");
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length == 0 || fileInfo.Length > MaximumFileBytes)
        {
            throw new InvalidDataException(
                $"O arquivo “{fileInfo.Name}” está vazio ou excede 50 MB.");
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var text = Decode(bytes);
        var rows = SemicolonCsvParser.Parse(text, MaximumRowsPerFile);
        if (rows.Count < 2)
        {
            throw new InvalidDataException(
                $"O arquivo “{fileInfo.Name}” não contém registros.");
        }

        var headers = rows[0]
            .Select(header => header.Trim().TrimStart('\uFEFF'))
            .ToArray();
        if (headers.Any(string.IsNullOrWhiteSpace)
            || headers.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != headers.Length)
        {
            throw new InvalidDataException(
                $"O cabeçalho de “{fileInfo.Name}” contém colunas vazias ou repetidas.");
        }

        var dataRows = new List<IReadOnlyDictionary<string, string>>();
        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = rows[rowIndex];
            if (values.Count == 1 && string.IsNullOrWhiteSpace(values[0]))
            {
                continue;
            }

            if (values.Count != headers.Length)
            {
                throw new InvalidDataException(
                    $"A linha {rowIndex + 1} de “{fileInfo.Name}” possui "
                    + $"{values.Count} campos; eram esperados {headers.Length}.");
            }

            var row = new Dictionary<string, string>(
                headers.Length,
                StringComparer.OrdinalIgnoreCase);
            for (var columnIndex = 0;
                 columnIndex < headers.Length;
                 columnIndex++)
            {
                row[headers[columnIndex]] = values[columnIndex].Trim();
            }

            dataRows.Add(row);
        }

        if (dataRows.Count == 0)
        {
            throw new InvalidDataException(
                $"O arquivo “{fileInfo.Name}” não contém linhas de dados.");
        }

        var kind = IdentifyKind(headers, fileInfo.Name);
        return new SemanticCsvDocument(
            path,
            kind,
            headers,
            dataRows,
            Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        try
        {
            return new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static SemanticCsvKind IdentifyKind(
        IReadOnlyCollection<string> headers,
        string fileName)
    {
        var set = headers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (set.Contains("ID_PONTO") && set.Contains("CODIGO_SEMANTICO"))
        {
            RequireHeaders(set, fileName, PointHeaders);
            return SemanticCsvKind.Points;
        }

        if (set.Contains("ID_COMPONENTE")
            && set.Contains("CLASSE_COMPONENTE"))
        {
            RequireHeaders(set, fileName, ComponentHeaders);
            return SemanticCsvKind.Components;
        }

        if (set.Contains("DIRECAO_BUILDER_SUGERIDA")
            && set.Contains("REVISAR_DIRECAO"))
        {
            RequireHeaders(set, fileName, DirectionHeaders);
            return SemanticCsvKind.Directions;
        }

        throw new InvalidDataException(
            $"O arquivo “{fileName}” não é um CSV semântico v07/v081 reconhecido.");
    }

    private static void RequireHeaders(
        IReadOnlySet<string> headers,
        string fileName,
        IReadOnlyList<string> required)
    {
        var missing = required
            .Where(header => !headers.Contains(header))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidDataException(
                $"O arquivo “{fileName}” não contém as colunas obrigatórias: "
                + string.Join(", ", missing)
                + ".");
        }
    }

    private static SemanticCsvDocument? GetSingleDocument(
        IReadOnlyList<SemanticCsvDocument> documents,
        SemanticCsvKind kind,
        bool required)
    {
        var matches = documents.Where(document => document.Kind == kind).ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidDataException(
                $"Selecione somente um arquivo do tipo {GetKindLabel(kind)}.");
        }

        if (required && matches.Length == 0)
        {
            throw new InvalidDataException(
                "O CSV de pontos semânticos v07 é obrigatório.");
        }

        return matches.SingleOrDefault();
    }

    private static List<SemanticPoint> ParsePoints(
        SemanticCsvDocument document,
        Guid datasetId,
        CancellationToken cancellationToken)
    {
        var points = new List<SemanticPoint>(document.Rows.Count);
        var externalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var handles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in document.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var externalId = Required(row, "ID_PONTO");
            var handle = Required(row, "HANDLE_PONTO");
            if (!externalIds.Add(externalId) || !handles.Add(handle))
            {
                throw new InvalidDataException(
                    $"O CSV de pontos contém ID ou handle repetido: {externalId}/{handle}.");
            }

            var confidence = ParseConfidence(Required(row, "CONFIANCA"));
            points.Add(new SemanticPoint(
                Guid.NewGuid(),
                datasetId,
                externalId,
                handle,
                ParseDiscipline(Required(row, "DISCIPLINA")),
                Required(row, "CODIGO_SEMANTICO"),
                Required(row, "DESCRICAO"),
                ParseHeight(row, "ALTURA_CM"),
                Value(row, "ALTURA_CM"),
                confidence,
                Required(row, "FONTE"),
                Required(row, "LAYER_SEMANTICA"),
                Value(row, "NOME_BLOCO"),
                RequiredDouble(row, "X_INSERCAO"),
                RequiredDouble(row, "Y_INSERCAO"),
                RequiredDouble(row, "Z_INSERCAO"),
                RequiredDouble(row, "X_CENTRO"),
                RequiredDouble(row, "Y_CENTRO"),
                RequiredDouble(row, "Z_CENTRO"),
                RequiredDouble(row, "ROTACAO_GRAUS"),
                RequiredDouble(row, "ESCALA_X"),
                RequiredDouble(row, "ESCALA_Y"),
                RequiredDouble(row, "ESCALA_Z"),
                RequiredInt(row, "QTD_COMPONENTES_GRAFICOS"),
                RequiredInt(row, "QTD_TEXTOS_ASSOCIADOS"),
                Value(row, "TEXTOS_ASSOCIADOS"),
                RequiredInt(row, "INSUNITS_CODIGO"),
                Required(row, "UNIDADE_COORDENADAS"),
                confidence is SemanticConfidence.Low or SemanticConfidence.Unknown
                    ? SemanticReviewStatus.NeedsReview
                    : SemanticReviewStatus.Identified,
                null,
                null));
        }

        return points;
    }

    private static List<SemanticComponent> ParseComponents(
        SemanticCsvDocument document,
        Guid datasetId,
        IReadOnlyList<SemanticPoint> points,
        CancellationToken cancellationToken)
    {
        var pointIds = points
            .Select(point => point.ExternalId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var components = new List<SemanticComponent>(document.Rows.Count);
        var externalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in document.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var externalId = Required(row, "ID_COMPONENTE");
            var pointExternalId = Required(row, "ID_PONTO");
            if (!externalIds.Add(externalId))
            {
                throw new InvalidDataException(
                    $"O CSV de componentes contém ID repetido: {externalId}.");
            }

            if (!pointIds.Contains(pointExternalId))
            {
                throw new InvalidDataException(
                    $"O componente {externalId} referencia o ponto inexistente "
                    + $"{pointExternalId}.");
            }

            components.Add(new SemanticComponent(
                Guid.NewGuid(),
                datasetId,
                externalId,
                Required(row, "HANDLE_COMPONENTE"),
                pointExternalId,
                Required(row, "HANDLE_PONTO"),
                ParseDiscipline(Required(row, "DISCIPLINA")),
                Required(row, "CODIGO_PONTO"),
                Required(row, "LAYER_SEMANTICA"),
                Required(row, "CLASSE_COMPONENTE"),
                Required(row, "CATEGORIA_ORIGEM"),
                Required(row, "TIPO_OBJETO"),
                Value(row, "NOME_BLOCO"),
                Value(row, "CONTEUDO_TEXTO"),
                RequiredDouble(row, "DISTANCIA_ASSOCIACAO"),
                ParseConfidence(Required(row, "CONFIANCA_ASSOCIACAO")),
                RequiredDouble(row, "X_REFERENCIA"),
                RequiredDouble(row, "Y_REFERENCIA"),
                RequiredDouble(row, "Z_REFERENCIA"),
                RequiredDouble(row, "X_CENTRO"),
                RequiredDouble(row, "Y_CENTRO"),
                RequiredDouble(row, "Z_CENTRO"),
                RequiredDouble(row, "ROTACAO_GRAUS")));
        }

        return components;
    }

    private static List<SemanticDirectionDiagnostic> ParseDirections(
        SemanticCsvDocument document,
        Guid datasetId,
        CancellationToken cancellationToken)
    {
        var directions =
            new List<SemanticDirectionDiagnostic>(document.Rows.Count);
        var handles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in document.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var handle = Required(row, "HANDLE");
            if (!handles.Add(handle))
            {
                throw new InvalidDataException(
                    $"O CSV de direções contém handle repetido: {handle}.");
            }

            directions.Add(new SemanticDirectionDiagnostic(
                Guid.NewGuid(),
                datasetId,
                Required(row, "VERSAO"),
                handle,
                Value(row, "DIRECAO_CALIBRACAO"),
                Required(row, "LAYER"),
                Value(row, "NOME_BLOCO"),
                Value(row, "NOME_EFETIVO"),
                RequiredDouble(row, "X_INSERCAO"),
                RequiredDouble(row, "Y_INSERCAO"),
                RequiredDouble(row, "Z_INSERCAO"),
                RequiredDouble(row, "X_CENTRO"),
                RequiredDouble(row, "Y_CENTRO"),
                RequiredDouble(row, "Z_CENTRO"),
                RequiredDouble(row, "ROTACAO_GRAUS"),
                RequiredDouble(row, "ANGULO_EFETIVO_GRAUS"),
                Required(row, "DIRECAO_BUILDER_SUGERIDA"),
                Required(row, "TIPO_ANGULO_GRAFICO"),
                RequiredDouble(row, "DISTANCIA_CARDINAL_GRAUS"),
                RequiredDouble(row, "DISTANCIA_LIMITE_QUADRANTE_GRAUS"),
                ParseYesNo(Required(row, "REVISAR_DIRECAO")),
                RequiredDouble(row, "DX_RELATIVO_CM"),
                RequiredDouble(row, "DY_RELATIVO_CM"),
                RequiredDouble(row, "DZ_RELATIVO_CM"),
                Required(row, "ORIGEM_DESLOCAMENTO"),
                ParseYesNo(Required(row, "REVISAR_DIRECAO"))
                    ? SemanticReviewStatus.NeedsReview
                    : SemanticReviewStatus.Identified,
                null,
                null,
                null));
        }

        return directions;
    }

    private static void EnsureSingleDrawing(
        SemanticCsvDocument document,
        string expectedName,
        string expectedPath)
    {
        foreach (var row in document.Rows)
        {
            if (!Required(row, "ARQUIVO_DWG").Equals(
                    expectedName,
                    StringComparison.OrdinalIgnoreCase)
                || !Required(row, "CAMINHO_DWG").Equals(
                    expectedPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"O arquivo “{Path.GetFileName(document.Path)}” mistura dados "
                    + "de desenhos diferentes.");
            }
        }
    }

    private static IReadOnlyList<string> BuildWarnings(
        SemanticCsvDocument? components,
        SemanticCsvDocument? directions,
        SemanticDataset dataset)
    {
        var warnings = new List<string>();
        if (components is null)
        {
            warnings.Add(
                "O CSV de componentes não foi selecionado; associações ficaram vazias.");
        }

        if (directions is null)
        {
            warnings.Add(
                "O CSV v081 de direções não foi selecionado; a revisão de tomadas ficou vazia.");
        }

        if (components is not null
            && directions is not null
            && !dataset.MatchesHistoricalBaseline)
        {
            warnings.Add(
                "Os totais importados diferem da linha de base histórica "
                + "272/196/76/100/35/38/2.");
        }

        return warnings;
    }

    private static string CalculateCombinedFingerprint(
        IReadOnlyList<SemanticCsvDocument> documents)
    {
        var manifest = string.Join(
            "\n",
            documents
                .OrderBy(document => document.Kind)
                .ThenBy(document => document.Path, StringComparer.OrdinalIgnoreCase)
                .Select(document =>
                    $"{document.Kind}:{Path.GetFileName(document.Path)}:{document.Sha256}"));
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(manifest)));
    }

    private static string InferVersion(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
        if (name.Contains("V07", StringComparison.Ordinal))
        {
            return "0.7.0";
        }

        return "não informada";
    }

    private static SemanticDiscipline ParseDiscipline(string value) =>
        Normalize(value) switch
        {
            "ELETRICO" => SemanticDiscipline.Electrical,
            "HIDRAULICO" => SemanticDiscipline.Hydraulic,
            _ => throw new InvalidDataException(
                $"Disciplina semântica desconhecida: “{value}”.")
        };

    private static SemanticConfidence ParseConfidence(string value) =>
        Normalize(value) switch
        {
            "ALTA" => SemanticConfidence.High,
            "MEDIA" => SemanticConfidence.Medium,
            "BAIXA" => SemanticConfidence.Low,
            "" => SemanticConfidence.Unknown,
            _ => throw new InvalidDataException(
                $"Confiança semântica desconhecida: “{value}”.")
        };

    private static bool ParseYesNo(string value) =>
        Normalize(value) switch
        {
            "SIM" => true,
            "NAO" => false,
            _ => throw new InvalidDataException(
                $"Valor SIM/NÃO desconhecido: “{value}”.")
        };

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Required(
        IReadOnlyDictionary<string, string> row,
        string column)
    {
        var value = Value(row, column);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException(
                $"A coluna {column} contém um valor obrigatório vazio.");
    }

    private static string Value(
        IReadOnlyDictionary<string, string> row,
        string column) =>
        row.TryGetValue(column, out var value)
            ? value
            : throw new InvalidDataException(
                $"A coluna obrigatória {column} não foi encontrada.");

    private static double RequiredDouble(
        IReadOnlyDictionary<string, string> row,
        string column)
    {
        var value = Required(row, column);
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            && double.IsFinite(parsed)
                ? parsed
                : throw new InvalidDataException(
                    $"O valor “{value}” da coluna {column} não é um número válido.");
    }

    private static double? ParseHeight(
        IReadOnlyDictionary<string, string> row,
        string column)
    {
        var value = Value(row, column);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            && double.IsFinite(parsed)
                ? parsed
                : null;
    }

    private static int RequiredInt(
        IReadOnlyDictionary<string, string> row,
        string column)
    {
        var value = Required(row, column);
        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            && parsed >= 0
                ? parsed
                : throw new InvalidDataException(
                    $"O valor “{value}” da coluna {column} não é um inteiro válido.");
    }

    private static string GetKindLabel(SemanticCsvKind kind) => kind switch
    {
        SemanticCsvKind.Points => "pontos",
        SemanticCsvKind.Components => "componentes",
        SemanticCsvKind.Directions => "direções",
        _ => "desconhecido"
    };

    private static readonly string[] PointHeaders =
    [
        "ARQUIVO_DWG", "CAMINHO_DWG", "ID_PONTO", "HANDLE_PONTO",
        "DISCIPLINA", "CODIGO_SEMANTICO", "DESCRICAO", "ALTURA_CM",
        "CONFIANCA", "FONTE", "LAYER_SEMANTICA", "NOME_BLOCO",
        "X_INSERCAO", "Y_INSERCAO", "Z_INSERCAO", "X_CENTRO",
        "Y_CENTRO", "Z_CENTRO", "ROTACAO_GRAUS", "ESCALA_X",
        "ESCALA_Y", "ESCALA_Z", "QTD_COMPONENTES_GRAFICOS",
        "QTD_TEXTOS_ASSOCIADOS", "TEXTOS_ASSOCIADOS", "INSUNITS_CODIGO",
        "UNIDADE_COORDENADAS"
    ];

    private static readonly string[] ComponentHeaders =
    [
        "ARQUIVO_DWG", "CAMINHO_DWG", "ID_COMPONENTE", "HANDLE_COMPONENTE",
        "ID_PONTO", "HANDLE_PONTO", "DISCIPLINA", "CODIGO_PONTO",
        "LAYER_SEMANTICA", "CLASSE_COMPONENTE", "CATEGORIA_ORIGEM",
        "TIPO_OBJETO", "NOME_BLOCO", "CONTEUDO_TEXTO",
        "DISTANCIA_ASSOCIACAO", "CONFIANCA_ASSOCIACAO", "X_REFERENCIA",
        "Y_REFERENCIA", "Z_REFERENCIA", "X_CENTRO", "Y_CENTRO",
        "Z_CENTRO", "ROTACAO_GRAUS"
    ];

    private static readonly string[] DirectionHeaders =
    [
        "VERSAO", "ARQUIVO_DWG", "CAMINHO_DWG", "HANDLE",
        "DIRECAO_CALIBRACAO", "LAYER", "NOME_BLOCO", "NOME_EFETIVO",
        "X_INSERCAO", "Y_INSERCAO", "Z_INSERCAO", "X_CENTRO",
        "Y_CENTRO", "Z_CENTRO", "ROTACAO_GRAUS",
        "ANGULO_EFETIVO_GRAUS", "DIRECAO_BUILDER_SUGERIDA",
        "TIPO_ANGULO_GRAFICO", "DISTANCIA_CARDINAL_GRAUS",
        "DISTANCIA_LIMITE_QUADRANTE_GRAUS", "REVISAR_DIRECAO",
        "DX_RELATIVO_CM", "DY_RELATIVO_CM", "DZ_RELATIVO_CM",
        "ORIGEM_DESLOCAMENTO"
    ];

    private enum SemanticCsvKind
    {
        Points,
        Components,
        Directions
    }

    private sealed record SemanticCsvDocument(
        string Path,
        SemanticCsvKind Kind,
        IReadOnlyList<string> Headers,
        IReadOnlyList<IReadOnlyDictionary<string, string>> Rows,
        string Sha256);
}

internal static class SemicolonCsvParser
{
    public static IReadOnlyList<IReadOnlyList<string>> Parse(
        string content,
        int maximumDataRows)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (maximumDataRows < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDataRows));
        }

        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < content.Length
                        && content[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            if (character == '"')
            {
                if (field.Length != 0)
                {
                    throw new InvalidDataException(
                        "Aspas CSV encontradas no meio de um campo não delimitado.");
                }

                quoted = true;
            }
            else if (character == ';')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (character is '\r' or '\n')
            {
                if (character == '\r'
                    && index + 1 < content.Length
                    && content[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = [];
                if (rows.Count > maximumDataRows + 1)
                {
                    throw new InvalidDataException(
                        $"O CSV excede o limite de {maximumDataRows:N0} registros.");
                }
            }
            else
            {
                field.Append(character);
            }
        }

        if (quoted)
        {
            throw new InvalidDataException(
                "O CSV terminou com um campo entre aspas não fechado.");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
