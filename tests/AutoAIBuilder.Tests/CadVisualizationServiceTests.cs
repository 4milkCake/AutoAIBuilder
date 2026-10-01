using System.Security.Cryptography;
using AutoAIBuilder.Application.CadVisualization;
using AutoAIBuilder.Infrastructure.CadVisualization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class CadVisualizationServiceTests
{
    private string _directory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.CadVisualization.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Generate_UsesVerifiedCopyAndThenLoadsCache()
    {
        var source = Path.Combine(_directory, "source.dwg");
        await File.WriteAllTextAsync(source, "DWG original imutável");
        var originalHash = Hash(source);
        var exporter = new StubCadGeometryExporter();
        var service = new CadVisualizationService(
            exporter,
            storageRoot: Path.Combine(_directory, "storage"));
        var projectId = Guid.NewGuid();

        var generated = await service.GenerateAsync(projectId, source);
        var cached = await service.GenerateAsync(projectId, source);

        Assert.AreEqual(1, exporter.ExecutionCount);
        Assert.AreEqual(originalHash, Hash(source));
        Assert.AreEqual("DWG original imutável", await File.ReadAllTextAsync(source));
        Assert.AreEqual(2, generated.Primitives.Count);
        Assert.AreEqual(1, generated.Layers.Count);
        Assert.AreEqual(5, generated.Coverage.SourceEntityCount);
        Assert.AreEqual(2, generated.Coverage.ExportedEntityCount);
        Assert.AreEqual(1, generated.Coverage.RemainingBlockCount);
        Assert.IsFalse(generated.LoadedFromCache);
        Assert.IsTrue(cached.LoadedFromCache);
        Assert.IsTrue(File.Exists(cached.ArtifactPath));
        StringAssert.Contains(
            cached.ArtifactPath,
            Path.Combine("cache", "6"));
    }

    [TestMethod]
    public void Parser_RejectsArtifactWithoutGeometry()
    {
        var artifact = Path.Combine(_directory, "empty.aiv");
        File.WriteAllLines(
            artifact,
            ["AIV|1", "BOUNDS|0|0|10|10", "LAYER|0|7|1"]);

        Assert.ThrowsException<InvalidDataException>(
            () => new CadGeometryArtifactParser().Parse(artifact));
    }

    [TestMethod]
    public void Parser_ReadsVersionTwoTextMetadataAndNormalizesMText()
    {
        var artifact = Path.Combine(_directory, "text-v2.aiv");
        File.WriteAllLines(
            artifact,
            [
                "AIV|2",
                "BOUNDS|0|0|100|100",
                "LAYER|6 - TEXTO COTAS|7|1",
                @"TEXT|T1|6 - TEXTO COTAS|10|20|2.5|90|{\fKiona%7Cb0%7Ci0;prever conduíte\Ppara fita \S1#2; %25%25d}|MiddleCenter|80|KIONA|MTEXT",
                "COVERAGE|1|1|0"
            ]);

        var geometry = new CadGeometryArtifactParser().Parse(artifact);

        var text = geometry.Primitives.Single();
        Assert.AreEqual(CadPrimitiveKind.Text, text.Kind);
        Assert.AreEqual(
            $"prever conduíte{Environment.NewLine}para fita 1/2 °",
            text.Text);
        Assert.AreEqual(CadTextAttachment.MiddleCenter, text.TextAttachment);
        Assert.AreEqual(80d, text.TextWidth);
        Assert.AreEqual("KIONA", text.TextStyle);
        Assert.AreEqual("MTEXT", text.SourceObjectType);
        Assert.AreEqual(90d, text.RotationDegrees);
    }

    [TestMethod]
    public void TextNormalizer_DecodesFormattingFractionsAndSymbols()
    {
        var source =
            @"\pxqc;{\fKiona|b0|i0|c0|p2;a iluminação\Pdo jardim} "
            + @"\S1#2; \U+00B2 %%p %%c";

        var normalized = CadTextNormalizer.Normalize(source);

        Assert.AreEqual(
            $"a iluminação{Environment.NewLine}do jardim 1/2 ² ± Ø",
            normalized);
    }

    [TestMethod]
    public async Task TryLoadCached_ReturnsNullWhenSourceIsLocked()
    {
        var source = Path.Combine(_directory, "locked-source.dwg");
        await File.WriteAllTextAsync(source, "DWG em uso");
        var service = new CadVisualizationService(
            new StubCadGeometryExporter(),
            storageRoot: Path.Combine(_directory, "storage"));
        await using var lockStream = new FileStream(
            source,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        var cached = service.TryLoadCached(Guid.NewGuid(), source);

        Assert.IsNull(cached);
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed class StubCadGeometryExporter : ICadGeometryExporter
    {
        public int ExecutionCount { get; private set; }

        public CadExporterStatus GetStatus() => new(
            true,
            "AutoCAD de teste",
            "25.0",
            "stub.exe",
            "Autodesk, Inc.",
            "Disponível");

        public async Task ExportAsync(
            string sourceCopyPath,
            string artifactPath,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;
            Assert.AreEqual(
                "DWG original imutável",
                await File.ReadAllTextAsync(
                    sourceCopyPath,
                    cancellationToken));
            await File.WriteAllLinesAsync(
                artifactPath,
                [
                    "AIV|1",
                    "BOUNDS|0|0|100|50",
                    "LAYER|ARQ-PAREDES|7|1",
                    "LINE|10|ARQ-PAREDES|0|0|100|0",
                    "CIRCLE|11|ARQ-PAREDES|50|25|5",
                    "COVERAGE|5|2|1"
                ],
                cancellationToken);
        }
    }
}
