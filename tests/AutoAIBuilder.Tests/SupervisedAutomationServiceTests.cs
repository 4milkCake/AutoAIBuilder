using System.Security.Cryptography;
using AutoAIBuilder.Application.Automation.Preview;
using AutoAIBuilder.Application.Automation.Supervised;
using AutoAIBuilder.Domain.Semantics;
using AutoAIBuilder.Infrastructure.Automation.Supervised;
using AutoAIBuilder.Infrastructure.CadVisualization;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class SupervisedAutomationServiceTests
{
    private string _directory = null!;
    private string _sourcePath = null!;
    private string _historicalMaskPath = null!;
    private string _historicalPointsPath = null!;
    private string _legendPath = null!;
    private string _scriptsPath = null!;
    private string _outputPath = null!;
    private Guid _projectId;
    private string _sourceHash = null!;
    private string _historicalHash = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.Tests",
            Guid.NewGuid().ToString("N"));
        var input = Path.Combine(_directory, "input");
        _scriptsPath = Path.Combine(_directory, "scripts");
        _outputPath = Path.Combine(_directory, "runs");
        Directory.CreateDirectory(input);
        Directory.CreateDirectory(_scriptsPath);
        _sourcePath = Path.Combine(input, "ORIGINAL.dwg");
        _historicalMaskPath = Path.Combine(input, "MASCARA_V06.dwg");
        File.WriteAllBytes(_sourcePath, [1, 2, 3, 4]);
        File.WriteAllBytes(_historicalMaskPath, [8, 7, 6, 5]);
        _sourceHash = Hash(_sourcePath);
        _historicalHash = Hash(_historicalMaskPath);
        _historicalPointsPath = Path.Combine(input, "pontos-v07.csv");
        File.WriteAllText(
            _historicalPointsPath,
            "\"HANDLE_PONTO\";\"LAYER_SEMANTICA\"\n"
            + "\"A1\";\"PONTOS_ELE_TOMADA\"\n"
            + "\"B2\";\"PONTOS_HID_AGUA\"\n");
        _legendPath = Path.Combine(input, "legenda.csv");
        File.WriteAllText(
            _legendPath,
            "\"ARQUIVO\";\"HANDLE\"\n\"ORIGINAL.dwg\";\"10\"\n"
            + "\"ORIGINAL.dwg\";\"11\"\n");
        foreach (var name in new[]
                 {
                     "mascara_previsualizar_v04.lsp",
                     "mascara_camadas_v05.lsp",
                     "mascara_limpeza_v06.lsp"
                 })
        {
            File.WriteAllText(Path.Combine(_scriptsPath, name), "teste");
        }

        _projectId = Guid.NewGuid();
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
    public void Inspect_BlocksExecutionWhilePreviewHasPendingDecisions()
    {
        var service = new SupervisedAutomationService(
            new FakeSupervisedCadRunner());
        var request = CreateRequest(ready: false);

        var preflight = service.Inspect(request);

        Assert.IsFalse(preflight.CanExecute);
        Assert.AreEqual(
            SupervisedAutomationGateStatus.Blocked,
            preflight.Gates.Single(
                gate => gate.Code == "PREVIEW_APPROVED").Status);
        Assert.IsFalse(Directory.Exists(_outputPath));
        Assert.AreEqual(_sourceHash, Hash(_sourcePath));
    }

    [TestMethod]
    public void Inspect_BlocksWhenOriginalAndHistoricalReferenceAreTheSame()
    {
        var service = new SupervisedAutomationService(
            new FakeSupervisedCadRunner());
        var request = CreateRequest(ready: true) with
        {
            HistoricalMaskPath = _sourcePath
        };

        var preflight = service.Inspect(request);

        Assert.IsFalse(preflight.CanExecute);
        Assert.AreEqual(
            SupervisedAutomationGateStatus.Blocked,
            preflight.Gates.Single(
                gate => gate.Code == "SOURCE_REFERENCE_DISTINCT").Status);
    }

    [TestMethod]
    public void Inspect_UsesPreservationModeWithoutCleanupCatalog()
    {
        var service = new SupervisedAutomationService(
            new FakeSupervisedCadRunner());

        var preflight = service.Inspect(CreateRequest(ready: true));

        Assert.IsTrue(preflight.CanExecute);
        Assert.AreEqual(
            SupervisedAutomationGateStatus.Passed,
            preflight.Gates.Single(
                gate => gate.Code == "PRESERVATION_MODE").Status);
        Assert.IsFalse(preflight.Gates.Any(
            gate => gate.Code == "AUDITED_CLEANUP"));
    }

    [TestMethod]
    public async Task Execute_CreatesIsolatedResultAndPreservesProtectedDwgs()
    {
        var service = new SupervisedAutomationService(
            new FakeSupervisedCadRunner());
        var request = CreateRequest(ready: true);

        var result = await service.ExecuteAsync(request);

        Assert.IsTrue(File.Exists(result.ResultDwgPath));
        Assert.IsTrue(File.Exists(result.ManifestPath));
        Assert.IsTrue(result.ResultDwgPath.Contains(
            "PONTOS_PRESERVADOS",
            StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(_sourceHash, Hash(_sourcePath));
        Assert.AreEqual(_historicalHash, Hash(_historicalMaskPath));
        Assert.IsTrue(result.SourceIntegrityConfirmed);
        Assert.IsTrue(result.HistoricalReferenceIntegrityConfirmed);
        Assert.AreEqual(2, result.Metrics.ExpectedPoints);
        Assert.AreEqual(2, result.Metrics.CorrectPoints);
        Assert.AreEqual(100d, result.Metrics.AccuracyPercentage);
        Assert.AreEqual(2, result.Metrics.HistoricalMatches);
        Assert.AreEqual(10, result.Metrics.EntitiesBefore);
        Assert.AreEqual(10, result.Metrics.EntitiesAfter);
        Assert.AreEqual(0, result.Metrics.MissingEntityHandles);
        Assert.AreEqual(1, result.Metrics.ProtectedBlocksAfter);
        Assert.AreEqual(
            SupervisedAutomationProfiles.PreservationTotal,
            result.ExecutionProfile);
        Assert.AreNotEqual(
            result.TechnicalCopySha256Before,
            result.ResultSha256);
        var restored = service.LoadLatest(_projectId, _outputPath);
        Assert.IsNotNull(restored);
        Assert.AreEqual(result.RunId, restored.RunId);
        Assert.IsTrue(restored.AcceptancePassed);
    }

    private SupervisedAutomationRequest CreateRequest(bool ready)
    {
        var points = new[]
        {
            CreatePoint("A1", "PONTOS_ELE_TOMADA"),
            CreatePoint("B2", "PONTOS_HID_AGUA")
        };
        var plan = new AutomationPreviewPlan(
            new string('A', 64),
            _projectId,
            Guid.NewGuid(),
            _sourcePath,
            _sourceHash,
            new string('B', 64),
            DateTimeOffset.UtcNow,
            [],
            true,
            ready ? 2 : 3,
            ready ? 2 : 2,
            0,
            ready ? 0 : 1,
            ready,
            "Antes",
            "Depois",
            "Seguro");
        return new SupervisedAutomationRequest(
            _projectId,
            plan,
            points,
            [],
            _sourcePath,
            _historicalMaskPath,
            _historicalPointsPath,
            _legendPath,
            _scriptsPath,
            _outputPath);
    }

    private SemanticPoint CreatePoint(string handle, string layer) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"ELE-{handle}",
            handle,
            layer.Contains("HID", StringComparison.Ordinal)
                ? SemanticDiscipline.Hydraulic
                : SemanticDiscipline.Electrical,
            "TESTE",
            "Ponto",
            null,
            string.Empty,
            SemanticConfidence.High,
            "v07",
            layer,
            "BLOCO",
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
            DateTimeOffset.UtcNow);

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed class FakeSupervisedCadRunner : ISupervisedCadRunner
    {
        public CadExporterStatus GetStatus() => new(
            true,
            "AutoCAD Core Console de teste",
            "1.0",
            "accoreconsole.exe",
            "Autodesk",
            "Disponível");

        public async Task<SupervisedCadRunnerResult> RunAsync(
            SupervisedCadRunnerRequest request,
            CancellationToken cancellationToken)
        {
            await File.AppendAllTextAsync(
                request.WorkingDwgPath,
                "resultado",
                cancellationToken);
            var report = Path.Combine(
                request.RunDirectory,
                "resultado-pontos.csv");
            var log = Path.Combine(request.RunDirectory, "processo.log");
            var preservation = Path.Combine(
                request.RunDirectory,
                "resultado-preservacao.csv");
            await File.WriteAllTextAsync(
                report,
                "\"HANDLE\";\"LAYER\";\"KIND\";\"STATUS\"\n"
                + "\"A1\";\"PONTOS_ELE_TOMADA\";\"PONTO\";\"OK\"\n"
                + "\"B2\";\"PONTOS_HID_AGUA\";\"PONTO\";\"OK\"\n",
                cancellationToken);
            await File.WriteAllTextAsync(log, "ok", cancellationToken);
            await File.WriteAllTextAsync(
                preservation,
                "\"ENTIDADES_ANTES\";\"ENTIDADES_DEPOIS\";"
                + "\"HANDLES_AUSENTES\";\"CINZA_PONTOS_ANTES\";"
                + "\"CINZA_PONTOS_DEPOIS\"\n"
                + "\"10\";\"10\";\"0\";\"1\";\"1\"\n",
                cancellationToken);
            return new SupervisedCadRunnerResult(
                report,
                preservation,
                log);
        }
    }
}
