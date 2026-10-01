using System.Globalization;
using AutoAIBuilder.Application.Recognition;
using AutoAIBuilder.Application.Semantics;
using AutoAIBuilder.Domain.Recognition;
using AutoAIBuilder.Domain.Semantics;
using AutoAIBuilder.Infrastructure.Recognition;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class CadRecognitionServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "AutoAIBuilder.Tests",
        Guid.NewGuid().ToString("N"));

    public CadRecognitionServiceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [TestMethod]
    public async Task AnalyzeUsesValidatedBlockProfileAndPreservesOriginal()
    {
        var source = Path.Combine(_directory, "novo.dwg");
        await File.WriteAllTextAsync(source, "DWG ORIGINAL");
        var service = new CadRecognitionService(
            new FakeInventoryExporter(
                [
                    "AAR|1",
                    "ENTITY|1|INSERT|ARQ-PONTOS|FZR|100|200|0|90|1|1|1|",
                    "ENTITY|A2|INSERT|MOBILIARIO|SOFA|300|400|0|0|1|1|1|",
                    "SUMMARY|2|2"
                ]),
            storageRoot: Path.Combine(_directory, "recognition"));
        var reference = CreateReference("FZR");

        var result = await service.AnalyzeAsync(
            Guid.NewGuid(),
            source,
            reference);

        Assert.IsTrue(result.OriginalIntegrityConfirmed);
        Assert.AreEqual("DWG ORIGINAL", await File.ReadAllTextAsync(source));
        Assert.AreEqual(2, result.InventoryEntityCount);
        Assert.AreEqual(2, result.Candidates.Count);
        var recognized = result.Candidates.Single(
            candidate => candidate.Handle == "1");
        Assert.AreEqual("ELE_AR_CONDICIONADO", recognized.ProposedCode);
        Assert.AreEqual(SemanticConfidence.High, recognized.Confidence);
        Assert.AreEqual(RecognitionCandidateStatus.Pending, recognized.Status);
        var unknown = result.Candidates.Single(
            candidate => candidate.Handle == "A2");
        Assert.AreEqual(SemanticConfidence.Unknown, unknown.Confidence);
        Assert.AreEqual(string.Empty, unknown.ProposedCode);
        Assert.IsNotNull(service.LoadLatest(result.ProjectId));
    }

    [TestMethod]
    public async Task ReviewPersistsApprovalAndHumanCorrection()
    {
        var source = Path.Combine(_directory, "revisao.dwg");
        await File.WriteAllTextAsync(source, "DWG");
        var service = new CadRecognitionService(
            new FakeInventoryExporter(
                [
                    "AAR|1",
                    "ENTITY|B1|INSERT|PONTOS|DESCONHECIDO|10|20|0|0|1|1|1|",
                    "SUMMARY|1|1"
                ]),
            storageRoot: Path.Combine(_directory, "recognition"));
        var projectId = Guid.NewGuid();
        var session = await service.AnalyzeAsync(
            projectId,
            source,
            SemanticWorkspaceSnapshot.Empty);
        var candidate = session.Candidates.Single();

        var corrected = service.Review(
            projectId,
            session.Id,
            candidate.Id,
            RecognitionCandidateStatus.Corrected,
            new RecognitionCorrectionRequest(
                SemanticDiscipline.Hydraulic,
                "HID_RALO",
                "Ralo",
                "PONTOS_HID_RALO",
                "PISO",
                "Confirmado visualmente."));

        var revised = corrected.Candidates.Single();
        Assert.AreEqual(RecognitionCandidateStatus.Corrected, revised.Status);
        Assert.AreEqual("HID_RALO", revised.ProposedCode);
        Assert.AreEqual(100, revised.ConfidenceScore);
        Assert.AreEqual(
            RecognitionCandidateStatus.Corrected,
            service.LoadLatest(projectId)!.Candidates.Single().Status);
        Assert.IsTrue(File.Exists(Path.Combine(
            session.RunDirectory,
            "auditoria-revisao.jsonl")));
    }

    [TestMethod]
    public async Task CalibrationDoesNotTreatEveryRepeatedBlockAsAValidatedPoint()
    {
        var source = Path.Combine(_directory, "legenda-e-ponto.dwg");
        await File.WriteAllTextAsync(source, "DWG");
        var service = new CadRecognitionService(
            new FakeInventoryExporter(
                [
                    "AAR|1",
                    "ENTITY|1|INSERT|PONTOS|FZR|10|20|0|0|1|1|1|",
                    "ENTITY|2|INSERT|LEGENDA|FZR|30|40|0|0|1|1|1|",
                    "SUMMARY|2|2"
                ]),
            storageRoot: Path.Combine(_directory, "recognition"));

        var result = await service.AnalyzeAsync(
            Guid.NewGuid(),
            source,
            CreateReference("FZR"));

        Assert.AreEqual(2, result.Candidates.Count);
        Assert.IsTrue(result.Candidates.All(candidate =>
            candidate.ConfidenceScore == 50));
        Assert.IsTrue(result.Candidates.All(candidate =>
            candidate.Confidence == SemanticConfidence.Unknown));
        StringAssert.Contains(
            result.Candidates[0].DetectionReason,
            "50%");
    }

    [TestMethod]
    public async Task LegendPairsSymbolWithDescriptionAndExcludesLegendExemplar()
    {
        var source = Path.Combine(_directory, "legenda-local.dwg");
        await File.WriteAllTextAsync(source, "DWG ORIGINAL");
        var service = new CadRecognitionService(
            new FakeInventoryExporter(
                [
                    "AAR|1",
                    "ENTITY|T0|MTEXT|TEXTOS||0|100|0|0|1|1|1|LEGENDA",
                    "ENTITY|T1|TEXT|TEXTOS||20|75|0|0|1|1|1|"
                    + "TOMADA AR CONDICIONADO",
                    "ENTITY|L1|INSERT|LEGENDA|FZR|10|75|0|0|1|1|1|",
                    "ENTITY|P1|INSERT|PONTOS|FZR|500|500|0|0|1|1|1|",
                    "ENTITY|M1|INSERT|MOBILIARIO|SOFA|700|500|0|0|1|1|1|",
                    "SUMMARY|5|3"
                ]),
            storageRoot: Path.Combine(_directory, "recognition"));

        var result = await service.AnalyzeAsync(
            Guid.NewGuid(),
            source,
            CreateReference("OUTRO_NOME_DE_BLOCO"));

        Assert.IsNotNull(result.LegendAnalysis);
        Assert.IsTrue(result.LegendAnalysis.IsDetected);
        Assert.AreEqual(1, result.LegendAnalysis.Entries.Count);
        Assert.AreEqual("FZR", result.LegendAnalysis.Entries[0].BlockName);
        Assert.AreEqual(
            "TOMADA AR CONDICIONADO",
            result.LegendAnalysis.Entries[0].Description);
        Assert.IsFalse(result.Candidates.Any(candidate =>
            candidate.Handle == "L1"));
        var planPoint = result.Candidates.Single(candidate =>
            candidate.Handle == "P1");
        Assert.IsTrue(planPoint.LegendMatched);
        Assert.AreEqual(82, planPoint.ConfidenceScore);
        Assert.AreEqual("ELE_AR_CONDICIONADO", planPoint.ProposedCode);
        StringAssert.Contains(
            planPoint.DetectionReason,
            "legenda do próprio DWG");
        Assert.IsTrue(File.Exists(Path.Combine(
            result.RunDirectory,
            "catalogo-legenda-11.6i.1c.json")));
        var manifest = await File.ReadAllTextAsync(Path.Combine(
            result.RunDirectory,
            "manifesto-11.6i.txt"));
        StringAssert.Contains(manifest, "LegendDetected=True");
        StringAssert.Contains(manifest, "LegendPairs=1");
        StringAssert.Contains(manifest, "LegendMatchedCandidates=1");
    }

    [TestMethod]
    public async Task NestedGeometrySignatureSeparatesSoundFromGas()
    {
        var source = Path.Combine(_directory, "som-e-gas.dwg");
        await File.WriteAllTextAsync(source, "DWG ORIGINAL");
        const string soundSignature = "L2P0C1A0V0S0T0I0O0R1.000";
        const string gasSignature = "L1P1C0A2V0S0T0I0O0R0.500";
        var service = new CadRecognitionService(
            new FakeInventoryExporter(
                [
                    "AAR|3",
                    Aar3Entity(
                        "T0",
                        "MTEXT",
                        "",
                        0,
                        100,
                        "LEGENDA"),
                    Aar3Entity(
                        "T1",
                        "TEXT",
                        "",
                        20,
                        90,
                        "SAÍDA DE SOM"),
                    Aar3Insert(
                        "LS",
                        "SIMBOLO_GENERICO",
                        10,
                        90,
                        1,
                        "ROOT-LEGENDA",
                        "ROOT-LEGENDA/0:SOM",
                        soundSignature),
                    Aar3Insert(
                        "PS",
                        "SIMBOLO_GENERICO",
                        500,
                        500,
                        1,
                        "ROOT-PLANTA-SOM",
                        "ROOT-PLANTA-SOM/0:SOM",
                        soundSignature),
                    Aar3Insert(
                        "PG",
                        "SIMBOLO_GENERICO",
                        600,
                        500,
                        1,
                        "ROOT-PLANTA-GAS",
                        "ROOT-PLANTA-GAS/0:GAS",
                        gasSignature),
                    Aar3Entity(
                        "F0",
                        "LINE",
                        "",
                        700,
                        500,
                        ""),
                    "SUMMARY|6|3|3|3"
                ]),
            storageRoot: Path.Combine(_directory, "recognition"));

        var result = await service.AnalyzeAsync(
            Guid.NewGuid(),
            source,
            SemanticWorkspaceSnapshot.Empty);

        var sound = result.Candidates.Single();
        Assert.AreEqual("PS", sound.Handle);
        Assert.AreEqual("SAÍDA DE SOM", sound.LegendDescription);
        Assert.AreEqual(soundSignature, sound.GeometrySignature);
        Assert.AreEqual(1, sound.ExpansionDepth);
        Assert.AreEqual("ROOT-PLANTA-SOM", sound.RootHandle);
        Assert.IsFalse(result.Candidates.Any(candidate =>
            candidate.Handle == "PG"));
        Assert.AreEqual(3, result.InventoryExpandedInsertCount);
    }

    [TestMethod]
    public void LegendInterpreterDoesNotInventLegendWithoutExplicitHeading()
    {
        var inventory = new CadEntityInventory(
            2,
            1,
            [
                CreateInventoryEntity(
                    "T1",
                    "TEXT",
                    text: "TOMADA BAIXA"),
                CreateInventoryEntity(
                    "B1",
                    "INSERT",
                    blockName: "TOMADA")
            ]);

        var result = new CadLegendInterpreter().Analyze(inventory);

        Assert.IsFalse(result.IsDetected);
        Assert.AreEqual(0, result.Entries.Count);
    }

    [TestMethod]
    public void LegendInterpreterRejectsExplicitSemanticContradiction()
    {
        var inventory = new CadEntityInventory(
            4,
            1,
            [
                CreateInventoryEntity(
                    "T0",
                    "MTEXT",
                    text: "LEGENDA",
                    y: 100),
                CreateInventoryEntity(
                    "T1",
                    "TEXT",
                    text: "CONDUÍTE P/ FITA DE LED",
                    x: 20,
                    y: 90),
                CreateInventoryEntity(
                    "B1",
                    "INSERT",
                    blockName: "PURIFICADOR",
                    x: 10,
                    y: 90),
                CreateInventoryEntity(
                    "F0",
                    "LINE",
                    x: 500,
                    y: 500)
            ]);

        var result = new CadLegendInterpreter().Analyze(inventory);

        Assert.IsTrue(result.IsDetected);
        Assert.AreEqual(1, result.DescriptionCandidateCount);
        Assert.AreEqual(0, result.Entries.Count);
        Assert.AreEqual(1, result.UnpairedDescriptionCount);
    }

    [TestMethod]
    public async Task AnalyzePersistsResolvedWcsAnchorAndOriginalInsertion()
    {
        var source = Path.Combine(_directory, "ancora-wcs.dwg");
        await File.WriteAllTextAsync(source, "DWG");
        var service = new CadRecognitionService(
            new FakeInventoryExporter(
                [
                    "AAR|2",
                    "ENTITY|P1|INSERT|ELETRICA|TOMADA|100|200|0|90|1|1|1||"
                    + "WCS|BOUNDS_CENTER_WCS|1000|2000|0|1|90|190|0|110|210|0",
                    "SUMMARY|1|1"
                ]),
            storageRoot: Path.Combine(_directory, "recognition"));

        var result = await service.AnalyzeAsync(
            Guid.NewGuid(),
            source,
            SemanticWorkspaceSnapshot.Empty);

        var candidate = result.Candidates.Single();
        Assert.AreEqual(100d, candidate.PositionX);
        Assert.AreEqual(200d, candidate.PositionY);
        Assert.AreEqual("WCS", candidate.CoordinateSystem);
        Assert.AreEqual("BOUNDS_CENTER_WCS", candidate.PositionSource);
        Assert.AreEqual(1000d, candidate.SourceInsertionX);
        Assert.AreEqual(2000d, candidate.SourceInsertionY);
        StringAssert.Contains(
            candidate.DetectionReason,
            "centro geométrico");
        var manifest = await File.ReadAllTextAsync(Path.Combine(
            result.RunDirectory,
            "manifesto-11.6i.txt"));
        StringAssert.Contains(manifest, "CoordinateSystemWcs=1");
        StringAssert.Contains(manifest, "CorrectedBlockAnchors=1");
    }

    [TestMethod]
    public void ParserRejectsInventoryWithoutVersionHeader()
    {
        var path = Path.Combine(_directory, "invalid.aar");
        File.WriteAllLines(path, ["SUMMARY|1|1"]);

        Assert.ThrowsException<InvalidDataException>(
            () => new CadEntityInventoryParser().Parse(path));
    }

    [TestMethod]
    public void ParserReadsVersionTwoSpatialContractAndKeepsLegacyCompatibility()
    {
        var versionTwoPath = Path.Combine(_directory, "spatial-v2.aar");
        File.WriteAllLines(
            versionTwoPath,
            [
                "AAR|2",
                "ENTITY|A1|INSERT|ELETRICA|TOMADA|100|200|0|90|1|1|1||"
                + "WCS|BOUNDS_CENTER_WCS|1000|2000|0|1|90|190|0|110|210|0",
                "SUMMARY|1|1"
            ]);
        var parser = new CadEntityInventoryParser();

        var versionTwo = parser.Parse(versionTwoPath).Entities.Single();

        Assert.AreEqual("WCS", versionTwo.CoordinateSystem);
        Assert.AreEqual("BOUNDS_CENTER_WCS", versionTwo.AnchorSource);
        Assert.AreEqual(100d, versionTwo.X);
        Assert.AreEqual(200d, versionTwo.Y);
        Assert.AreEqual(1000d, versionTwo.InsertionX);
        Assert.AreEqual(2000d, versionTwo.InsertionY);
        Assert.IsNotNull(versionTwo.Bounds);
        Assert.AreEqual(90d, versionTwo.Bounds.MinimumX);
        Assert.AreEqual(210d, versionTwo.Bounds.MaximumY);

        var legacyPath = Path.Combine(_directory, "spatial-v1.aar");
        File.WriteAllLines(
            legacyPath,
            [
                "AAR|1",
                "ENTITY|A1|INSERT|ELETRICA|TOMADA|10|20|0|0|1|1|1|",
                "SUMMARY|1|1"
            ]);

        var legacy = parser.Parse(legacyPath).Entities.Single();

        Assert.AreEqual("LEGACY_UNSPECIFIED", legacy.CoordinateSystem);
        Assert.AreEqual("LEGACY_ENTITY_POINT", legacy.AnchorSource);
        Assert.AreEqual(10d, legacy.InsertionX);
        Assert.IsNull(legacy.Bounds);
    }

    [TestMethod]
    public void ParserReadsVersionThreeHierarchyAndGeometryContract()
    {
        var path = Path.Combine(_directory, "hierarchy-v3.aar");
        File.WriteAllLines(
            path,
            [
                "AAR|3",
                Aar3Insert(
                    "N1",
                    "SOM",
                    100,
                    200,
                    2,
                    "ROOT1",
                    "ROOT1/0:BLOCO/2:SOM",
                    "L2P0C1A0V0S0T0I0O0R1.000"),
                "SUMMARY|1|1|0|1"
            ]);

        var inventory = new CadEntityInventoryParser().Parse(path);
        var entity = inventory.Entities.Single();

        Assert.AreEqual(2, entity.ExpansionDepth);
        Assert.AreEqual("ROOT1", entity.RootHandle);
        Assert.AreEqual("ROOT1/0:BLOCO/2:SOM", entity.StablePath);
        Assert.AreEqual("SOM", entity.RawBlockName);
        Assert.AreEqual(
            "L2P0C1A0V0S0T0I0O0R1.000",
            entity.GeometrySignature);
        Assert.AreEqual(3, entity.PrimitiveCount);
        Assert.AreEqual(0, entity.NestedInsertCount);
        Assert.AreEqual(0, inventory.TopLevelEntityCount);
        Assert.AreEqual(1, inventory.ExpandedEntityCount);
    }

    [TestMethod]
    public void ExpandedGeometryCorrectsRemoteRootAndPreservesOriginalInsertion()
    {
        var root = new CadInventoryEntity(
            "ROOT",
            "INSERT",
            "TESTE",
            "TOMADA",
            -1000,
            -2000,
            0,
            0,
            1,
            1,
            1,
            "",
            "WCS",
            "INSERTION_WCS",
            -1000,
            -2000,
            0,
            null,
            0,
            "ROOT",
            "ROOT",
            "TOMADA",
            "L1P0C0A0V0S0T0I0O0R0",
            1,
            0);
        var child = new CadInventoryEntity(
            "CHILD",
            "LINE",
            "TESTE",
            "",
            100,
            200,
            0,
            0,
            1,
            1,
            1,
            "",
            "WCS",
            "ENTITY_POINT_WCS",
            100,
            200,
            0,
            new CadInventoryBounds(90, 190, 0, 110, 210, 0),
            1,
            "ROOT",
            "ROOT/0:LINE");
        var inventory = new CadEntityInventory(2, 1, [root, child], 1, 1);

        var resolved = CadExpandedGeometryAnchorResolver.Resolve(inventory);
        var corrected = resolved.Entities.Single(entity =>
            entity.Handle == "ROOT");

        Assert.AreEqual(100d, corrected.X);
        Assert.AreEqual(200d, corrected.Y);
        Assert.AreEqual(-1000d, corrected.InsertionX);
        Assert.AreEqual(-2000d, corrected.InsertionY);
        Assert.AreEqual(
            "EXPANDED_GEOMETRY_CENTER_WCS",
            corrected.AnchorSource);
        Assert.IsNotNull(corrected.Bounds);
    }

    [TestMethod]
    public void EmbeddedAutoCadEncodersAdvancePastInsertedEscapeSequence()
    {
        var assembly = typeof(CadRecognitionService).Assembly;
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name =>
                name.EndsWith(
                    "AutoCadRecognitionInventory.lsp",
                    StringComparison.Ordinal)
                || name.EndsWith(
                    "AutoCadVisualizationExport.lsp",
                    StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(2, resourceNames.Length);
        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            Assert.IsNotNull(stream);
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();
            StringAssert.Contains(
                source,
                "vl-string-search old text start");
            StringAssert.Contains(
                source,
                "setq start (+ position (strlen replacement))");
            StringAssert.Contains(source, "'trans");
            StringAssert.Contains(source, "\"utf8\"");
        }

        var recognitionResource = resourceNames.Single(name =>
            name.EndsWith(
                "AutoCadRecognitionInventory.lsp",
                StringComparison.Ordinal));
        using var recognitionStream =
            assembly.GetManifestResourceStream(recognitionResource);
        Assert.IsNotNull(recognitionStream);
        using var recognitionReader = new StreamReader(recognitionStream);
        var recognitionSource = recognitionReader.ReadToEnd();
        StringAssert.Contains(recognitionSource, "AAR|3");
        StringAssert.Contains(recognitionSource, "BOUNDS_CENTER_WCS");
        StringAssert.Contains(recognitionSource, "aar:block-profile");
        StringAssert.Contains(recognitionSource, "aar:expand-insert");
        StringAssert.Contains(recognitionSource, "stable-path");
        StringAssert.Contains(recognitionSource, "aar:fallback-box");

        var visualizationResource = resourceNames.Single(name =>
            name.EndsWith(
                "AutoCadVisualizationExport.lsp",
                StringComparison.Ordinal));
        using var visualizationStream =
            assembly.GetManifestResourceStream(visualizationResource);
        Assert.IsNotNull(visualizationStream);
        using var visualizationReader =
            new StreamReader(visualizationStream);
        var visualizationSource = visualizationReader.ReadToEnd();
        StringAssert.Contains(visualizationSource, "AIV\" \"2");
        StringAssert.Contains(visualizationSource, "aiv:text-attachment");
        StringAssert.Contains(visualizationSource, "aiv:text-value");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static SemanticWorkspaceSnapshot CreateReference(string blockName)
    {
        var projectId = Guid.NewGuid();
        var datasetId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var point = new SemanticPoint(
            Guid.NewGuid(),
            datasetId,
            "ELE-1",
            "1",
            SemanticDiscipline.Electrical,
            "ELE_AR_CONDICIONADO",
            "Tomada para ar-condicionado",
            245,
            "245",
            SemanticConfidence.High,
            "BASE_VALIDADA",
            "PONTOS_ELE_AR_CONDICIONADO",
            blockName,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            string.Empty,
            4,
            "MILIMETROS",
            SemanticReviewStatus.Approved,
            null,
            now);
        var dataset = new SemanticDataset(
            datasetId,
            projectId,
            "referencia.dwg",
            "referencia.dwg",
            "v07",
            "ABC",
            [],
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            true,
            now,
            now);
        return new SemanticWorkspaceSnapshot(
            dataset,
            [point],
            [],
            [],
            [],
            []);
    }

    private static CadInventoryEntity CreateInventoryEntity(
        string handle,
        string objectType,
        string blockName = "",
        string text = "",
        double x = 0,
        double y = 0) =>
        new(
            handle,
            objectType,
            "TESTE",
            blockName,
            x,
            y,
            0,
            0,
            1,
            1,
            1,
            text,
            "WCS",
            "ENTITY_POINT_WCS",
            x,
            y,
            0,
            null);

    private static string Aar3Entity(
        string handle,
        string objectType,
        string blockName,
        double x,
        double y,
        string text) =>
        string.Join(
            "|",
            [
                "ENTITY",
                handle,
                objectType,
                "TESTE",
                blockName,
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture),
                "0",
                "0",
                "1",
                "1",
                "1",
                Uri.EscapeDataString(text),
                "WCS",
                "ENTITY_POINT_WCS",
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture),
                "0",
                "0",
                "0",
                "0",
                "0",
                "0",
                "0",
                "0",
                "0",
                handle,
                handle,
                "",
                "",
                "0",
                "0"
            ]);

    private static string Aar3Insert(
        string handle,
        string blockName,
        double x,
        double y,
        int expansionDepth,
        string rootHandle,
        string stablePath,
        string geometrySignature) =>
        string.Join(
            "|",
            [
                "ENTITY",
                handle,
                "INSERT",
                "TESTE",
                blockName,
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture),
                "0",
                "0",
                "1",
                "1",
                "1",
                "",
                "WCS",
                "INSERTION_WCS",
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture),
                "0",
                "0",
                "0",
                "0",
                "0",
                "0",
                "0",
                "0",
                expansionDepth.ToString(CultureInfo.InvariantCulture),
                rootHandle,
                Uri.EscapeDataString(stablePath),
                blockName,
                geometrySignature,
                "3",
                "0"
            ]);

    private sealed class FakeInventoryExporter(
        IReadOnlyList<string> lines) : ICadEntityInventoryExporter
    {
        public CadEntityInventoryExporterStatus GetStatus() => new(
            true,
            "AutoCAD Core Console",
            "25.0",
            "fake.exe",
            "Autodesk",
            "Disponível.");

        public Task ExportAsync(
            string sourceCopyPath,
            string inventoryPath,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            File.WriteAllLines(inventoryPath, lines);
            return Task.CompletedTask;
        }
    }
}
