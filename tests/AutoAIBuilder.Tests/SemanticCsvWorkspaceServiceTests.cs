using System.Text;
using AutoAIBuilder.Domain.Semantics;
using AutoAIBuilder.Infrastructure.Persistence;
using AutoAIBuilder.Infrastructure.Semantics;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class SemanticCsvWorkspaceServiceTests
{
    private string _directory = null!;
    private string _databasePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.Semantics.Tests",
            Guid.NewGuid().ToString("N"));
        _databasePath = Path.Combine(_directory, "data", "autoaibuilder.db");
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Import_PersistsStructuredDataAndPreservesReviewOnReimport()
    {
        var files = WriteValidSample(useLatin1: true);
        var originalBytes = files.ToDictionary(
            path => path,
            File.ReadAllBytes,
            StringComparer.OrdinalIgnoreCase);
        var projectId = Guid.NewGuid();
        var repository = new SqliteSemanticDatasetRepository(
            new SqliteDatabase(_databasePath));
        var service = new SemanticCsvWorkspaceService(repository);

        var first = await service.ImportAsync(projectId, files);

        Assert.IsFalse(first.ReplacedExistingDataset);
        Assert.AreEqual(2, first.Dataset.PointCount);
        Assert.AreEqual(1, first.Dataset.ElectricalPointCount);
        Assert.AreEqual(1, first.Dataset.HydraulicPointCount);
        Assert.AreEqual(1, first.Dataset.ComponentCount);
        Assert.AreEqual(2, first.Dataset.SemanticLayerCount);
        Assert.AreEqual(1, first.Dataset.DirectionCount);
        Assert.AreEqual(1, first.Dataset.DirectionReviewCount);
        Assert.AreEqual(64, first.Dataset.SourceFingerprint.Length);
        Assert.IsFalse(first.Dataset.MatchesHistoricalBaseline);

        var snapshot = service.GetForProject(projectId);
        Assert.AreEqual(2, snapshot.Points.Count);
        Assert.AreEqual("PONTO LÓGICA", snapshot.Points[0].Description);
        Assert.AreEqual(1, snapshot.Components.Count);
        Assert.AreEqual(1, snapshot.Directions.Count);

        var approvedPoint = snapshot.Points.Single(
            point => point.ExternalId == "ELE-001");
        service.UpdatePointReview(
            projectId,
            approvedPoint.Id,
            SemanticReviewStatus.Approved);

        var second = await service.ImportAsync(projectId, files);
        var reloaded = service.GetForProject(projectId);

        Assert.IsTrue(second.ReplacedExistingDataset);
        Assert.AreEqual(
            SemanticReviewStatus.Approved,
            reloaded.Points.Single(
                point => point.ExternalId == "ELE-001").ReviewStatus);
        foreach (var file in files)
        {
            CollectionAssert.AreEqual(
                originalBytes[file],
                File.ReadAllBytes(file),
                $"A origem foi alterada: {file}");
        }
    }

    [TestMethod]
    public async Task Import_RejectsOrphanComponentWithoutPersistingPartialData()
    {
        var files = WriteValidSample(useLatin1: false);
        var componentPath = files.Single(
            path => path.Contains("componentes", StringComparison.Ordinal));
        var content = File.ReadAllText(componentPath, Encoding.UTF8)
            .Replace("\"ELE-001\"", "\"ELE-INEXISTENTE\"");
        File.WriteAllText(componentPath, content, Encoding.UTF8);
        var projectId = Guid.NewGuid();
        var service = new SemanticCsvWorkspaceService(
            new SqliteSemanticDatasetRepository(
                new SqliteDatabase(_databasePath)));

        await Assert.ThrowsExceptionAsync<InvalidDataException>(
            () => service.ImportAsync(projectId, files));

        Assert.IsNull(service.GetForProject(projectId).Dataset);
    }

    [TestMethod]
    public async Task Correction_CreatesProjectKnowledgeSurvivesReimportAndCanUndo()
    {
        var files = WriteValidSample(useLatin1: false);
        var projectId = Guid.NewGuid();
        var service = new SemanticCsvWorkspaceService(
            new SqliteSemanticDatasetRepository(
                new SqliteDatabase(_databasePath)));
        await service.ImportAsync(projectId, files);
        var original = service.GetForProject(projectId).Points.Single(
            point => point.ExternalId == "ELE-001");

        service.CorrectPoint(
            projectId,
            original.Id,
            new(
                SemanticDiscipline.Electrical,
                "ELE_TOMADA_AR_CONDICIONADO",
                "TOMADA PARA AR-CONDICIONADO",
                "245",
                "Confirmado no detalhe do arquiteto."));

        var correctedSnapshot = service.GetForProject(projectId);
        var corrected = correctedSnapshot.Points.Single(
            point => point.ExternalId == "ELE-001");
        Assert.AreEqual(
            SemanticReviewStatus.Corrected,
            corrected.ReviewStatus);
        Assert.AreEqual(
            "ELE_TOMADA_AR_CONDICIONADO",
            corrected.SemanticCode);
        Assert.AreEqual(245d, corrected.HeightCm);
        Assert.AreEqual(1, correctedSnapshot.Revisions.Count);
        Assert.AreEqual(1, correctedSnapshot.KnowledgeEntries.Count);

        await service.ImportAsync(projectId, files);
        var reimported = service.GetForProject(projectId);
        Assert.AreEqual(
            "ELE_TOMADA_AR_CONDICIONADO",
            reimported.Points.Single(
                point => point.ExternalId == "ELE-001").SemanticCode);
        Assert.AreEqual(1, reimported.Revisions.Count);
        Assert.AreEqual(1, reimported.KnowledgeEntries.Count);

        Assert.IsTrue(service.UndoLatestRevision(
            projectId,
            SemanticReviewEntityKind.Point,
            corrected.Id));
        var undone = service.GetForProject(projectId);
        var restored = undone.Points.Single(
            point => point.ExternalId == "ELE-001");
        Assert.AreEqual("ELE_TOMADA_DADOS", restored.SemanticCode);
        Assert.AreEqual(
            SemanticReviewStatus.Identified,
            restored.ReviewStatus);
        Assert.AreEqual(0, undone.KnowledgeEntries.Count);
        Assert.IsNotNull(undone.Revisions.Single().RevertedAt);
    }

    [TestMethod]
    public async Task SimilarCorrection_UpdatesOnlyConfirmedCandidatesWithHistory()
    {
        var files = WriteValidSample(useLatin1: false);
        MakeSecondPointSimilar(files);
        var projectId = Guid.NewGuid();
        var service = new SemanticCsvWorkspaceService(
            new SqliteSemanticDatasetRepository(
                new SqliteDatabase(_databasePath)));
        await service.ImportAsync(projectId, files);
        var source = service.GetForProject(projectId).Points.Single(
            point => point.ExternalId == "ELE-001");
        var similar = service.FindSimilarPoints(projectId, source.Id);
        Assert.AreEqual(1, similar.Count);
        Assert.AreEqual("ELE-002", similar[0].ExternalId);

        service.CorrectPoint(
            projectId,
            source.Id,
            new(
                SemanticDiscipline.Electrical,
                "ELE_TOMADA_AR_CONDICIONADO",
                "TOMADA PARA AR-CONDICIONADO",
                "245",
                "Padrão confirmado."));
        var updated = service.ApplyPointCorrection(
            projectId,
            source.Id,
            [similar[0].Id],
            "Aplicação supervisionada.");

        var snapshot = service.GetForProject(projectId);
        Assert.AreEqual(1, updated);
        Assert.AreEqual(
            "ELE_TOMADA_AR_CONDICIONADO",
            snapshot.Points.Single(
                point => point.ExternalId == "ELE-002").SemanticCode);
        Assert.AreEqual(2, snapshot.Revisions.Count);
        Assert.AreEqual(2, snapshot.KnowledgeEntries.Single().EvidenceCount);
    }

    [TestMethod]
    public async Task DirectionReview_IsPersistedAndCanBeUndone()
    {
        var files = WriteValidSample(useLatin1: false);
        var projectId = Guid.NewGuid();
        var service = new SemanticCsvWorkspaceService(
            new SqliteSemanticDatasetRepository(
                new SqliteDatabase(_databasePath)));
        await service.ImportAsync(projectId, files);
        var direction = service.GetForProject(projectId).Directions.Single();

        service.ReviewDirection(
            projectId,
            direction.Id,
            new(
                "ESQUERDA",
                SemanticReviewStatus.Corrected,
                "Validado visualmente."));

        var corrected = service.GetForProject(projectId);
        Assert.AreEqual(
            "ESQUERDA",
            corrected.Directions.Single().EffectiveBuilderDirection);
        Assert.AreEqual(
            SemanticReviewStatus.Corrected,
            corrected.Directions.Single().ReviewStatus);
        Assert.AreEqual(1, corrected.Revisions.Count);

        Assert.IsTrue(service.UndoLatestRevision(
            projectId,
            SemanticReviewEntityKind.Direction,
            direction.Id));
        var restored = service.GetForProject(projectId).Directions.Single();
        Assert.AreEqual("DIREITA", restored.EffectiveBuilderDirection);
        Assert.AreEqual(
            SemanticReviewStatus.NeedsReview,
            restored.ReviewStatus);
    }

    [TestMethod]
    public async Task HistoricalArtifacts_ReproduceAuditedBaselineWhenAvailable()
    {
        var root = FindHistoricalRoot();
        if (!Directory.Exists(root))
        {
            return;
        }

        var allCsv = Directory.GetFiles(
            root,
            "*.csv",
            SearchOption.AllDirectories);
        var points = FindSingle(
            allCsv,
            "TESTE_01_MASCARA_AUTOMATICA_V06_pontos_semanticos_v07.csv");
        var components = FindSingle(
            allCsv,
            "TESTE_01_MASCARA_AUTOMATICA_V06_componentes_semanticos_v07.csv");
        var directions = FindSingle(
            allCsv,
            "TESTE_01_MASCARA_AUTOMATICA_V06_diagnostico_direcoes_v081.csv");
        if (points is null || components is null || directions is null)
        {
            return;
        }

        var service = new SemanticCsvWorkspaceService(
            new SqliteSemanticDatasetRepository(
                new SqliteDatabase(_databasePath)));
        var result = await service.ImportAsync(
            Guid.NewGuid(),
            [points, components, directions]);

        Assert.IsTrue(
            result.Dataset.MatchesHistoricalBaseline,
            "Os artefatos históricos deixaram de reproduzir a linha de base.");
        Assert.AreEqual(272, result.Dataset.PointCount);
        Assert.AreEqual(196, result.Dataset.ElectricalPointCount);
        Assert.AreEqual(76, result.Dataset.HydraulicPointCount);
        Assert.AreEqual(100, result.Dataset.ComponentCount);
        Assert.AreEqual(35, result.Dataset.SemanticLayerCount);
        Assert.AreEqual(38, result.Dataset.DirectionCount);
        Assert.AreEqual(2, result.Dataset.DirectionReviewCount);
        Assert.AreEqual(15, result.Dataset.BuilderValidatedDirectionCount);
        Assert.AreEqual(23, result.Dataset.SymmetryInferredDirectionCount);
    }

    private IReadOnlyList<string> WriteValidSample(bool useLatin1)
    {
        var pointsPath = Path.Combine(_directory, "pontos_semanticos_v07.csv");
        var componentsPath =
            Path.Combine(_directory, "componentes_semanticos_v07.csv");
        var directionsPath =
            Path.Combine(_directory, "diagnostico_direcoes_v081.csv");
        var drawingPath = Path.Combine(_directory, "projeto.dwg");
        var points =
            """
            "ARQUIVO_DWG";"CAMINHO_DWG";"ID_PONTO";"HANDLE_PONTO";"DISCIPLINA";"CODIGO_SEMANTICO";"DESCRICAO";"ALTURA_CM";"CONFIANCA";"FONTE";"LAYER_SEMANTICA";"NOME_BLOCO";"X_INSERCAO";"Y_INSERCAO";"Z_INSERCAO";"X_CENTRO";"Y_CENTRO";"Z_CENTRO";"ROTACAO_GRAUS";"ESCALA_X";"ESCALA_Y";"ESCALA_Z";"QTD_COMPONENTES_GRAFICOS";"QTD_TEXTOS_ASSOCIADOS";"TEXTOS_ASSOCIADOS";"INSUNITS_CODIGO";"UNIDADE_COORDENADAS"
            "projeto.dwg";"{DRAWING}";"ELE-001";"A1";"ELETRICO";"ELE_TOMADA_DADOS";"PONTO LÓGICA";"55";"ALTA";"LEGENDA_DWG";"PONTOS_ELE";"BLOCO_E";"10";"20";"0";"11";"21";"0";"0";"1";"1";"1";"1";"1";"20A";"4";"MILIMETROS"
            "projeto.dwg";"{DRAWING}";"HID-001";"B1";"HIDRAULICO";"HID_AGUA_FRIA";"ÁGUA FRIA";"";"MEDIA";"LEGENDA_DWG";"PONTOS_HID";"BLOCO_H";"30";"40";"0";"31";"41";"0";"90";"1";"1";"1";"0";"0";"";"4";"MILIMETROS"
            """.Replace("{DRAWING}", drawingPath);
        var components =
            """
            "ARQUIVO_DWG";"CAMINHO_DWG";"ID_COMPONENTE";"HANDLE_COMPONENTE";"ID_PONTO";"HANDLE_PONTO";"DISCIPLINA";"CODIGO_PONTO";"LAYER_SEMANTICA";"CLASSE_COMPONENTE";"CATEGORIA_ORIGEM";"TIPO_OBJETO";"NOME_BLOCO";"CONTEUDO_TEXTO";"DISTANCIA_ASSOCIACAO";"CONFIANCA_ASSOCIACAO";"X_REFERENCIA";"Y_REFERENCIA";"Z_REFERENCIA";"X_CENTRO";"Y_CENTRO";"Z_CENTRO";"ROTACAO_GRAUS"
            "projeto.dwg";"{DRAWING}";"CMP-001";"C1";"ELE-001";"A1";"ELETRICO";"ELE_TOMADA_DADOS";"PONTOS_ELE";"TEXTO";"REF_ELE_ANOTACAO";"TEXT";"";"20A";"2.5";"ALTA";"10";"20";"0";"11";"21";"0";"0"
            """.Replace("{DRAWING}", drawingPath);
        var directions =
            """
            "VERSAO";"ARQUIVO_DWG";"CAMINHO_DWG";"HANDLE";"DIRECAO_CALIBRACAO";"LAYER";"NOME_BLOCO";"NOME_EFETIVO";"X_INSERCAO";"Y_INSERCAO";"Z_INSERCAO";"X_CENTRO";"Y_CENTRO";"Z_CENTRO";"ROTACAO_GRAUS";"ESCALA_X";"ESCALA_Y";"ESCALA_Z";"ANGULO_EFETIVO_GRAUS";"DIRECAO_BUILDER_SUGERIDA";"TIPO_ANGULO_GRAFICO";"DISTANCIA_CARDINAL_GRAUS";"DISTANCIA_LIMITE_QUADRANTE_GRAUS";"REVISAR_DIRECAO";"DX_RELATIVO_CM";"DY_RELATIVO_CM";"DZ_RELATIVO_CM";"ORIGEM_DESLOCAMENTO";"QTD_PROPRIEDADES_DINAMICAS";"PROPRIEDADES_DINAMICAS"
            "0.8.1";"projeto.dwg";"{DRAWING}";"D1";"";"PONTOS_ELE";"EGT";"EGT";"10";"20";"0";"11";"21";"0";"42.2";"1";"1";"1";"42.2";"DIREITA";"INCLINADO_GRAFICAMENTE";"42.2";"2.8";"SIM";"-2.4";"1.5";"0";"VALIDADO_NO_BUILDER";"0";""
            """.Replace("{DRAWING}", drawingPath);

        WriteText(pointsPath, points, useLatin1);
        WriteText(componentsPath, components, useLatin1);
        WriteText(directionsPath, directions, useLatin1);
        return [pointsPath, componentsPath, directionsPath];
    }

    private static void WriteText(string path, string content, bool useLatin1)
    {
        var encoding = useLatin1 ? Encoding.Latin1 : Encoding.UTF8;
        File.WriteAllBytes(path, encoding.GetBytes(content));
    }

    private static void MakeSecondPointSimilar(
        IReadOnlyList<string> files)
    {
        var pointsPath = files.Single(path =>
            path.Contains("pontos_semanticos", StringComparison.Ordinal));
        var content = File.ReadAllText(pointsPath, Encoding.UTF8);
        content = content
            .Replace("\"HID-001\"", "\"ELE-002\"")
            .Replace("\"HIDRAULICO\"", "\"ELETRICO\"")
            .Replace("\"HID_AGUA_FRIA\"", "\"ELE_TOMADA_DADOS\"")
            .Replace("\"ÁGUA FRIA\"", "\"PONTO LÓGICA\"")
            .Replace("\"PONTOS_HID\"", "\"PONTOS_ELE\"")
            .Replace("\"BLOCO_H\"", "\"BLOCO_E\"");
        File.WriteAllText(pointsPath, content, Encoding.UTF8);
    }

    private static string? FindSingle(
        IEnumerable<string> paths,
        string fileName) =>
        paths.SingleOrDefault(path =>
            Path.GetFileName(path).Equals(
                fileName,
                StringComparison.OrdinalIgnoreCase));

    private static string FindHistoricalRoot()
    {
        var candidates = new List<string>
        {
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "Automacao_CAD")
        };
        var oneDrive = Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrWhiteSpace(oneDrive))
        {
            candidates.Add(Path.Combine(oneDrive, "Documentos", "Automacao_CAD"));
            candidates.Add(Path.Combine(oneDrive, "Documents", "Automacao_CAD"));
        }

        return candidates.FirstOrDefault(Directory.Exists)
            ?? candidates[0];
    }
}
