using System.Security.Cryptography;
using AutoAIBuilder.Application.Automation.Preview;
using AutoAIBuilder.Application.CadVisualization;
using AutoAIBuilder.Application.Semantics;
using AutoAIBuilder.Domain.Semantics;
using AutoAIBuilder.Infrastructure.Automation;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class AutomationPreviewServiceTests
{
    private string _directory = null!;
    private string _drawingPath = null!;
    private Guid _projectId;
    private Guid _datasetId;
    private string _drawingHash = null!;
    private InMemoryDecisionRepository _repository = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _drawingPath = Path.Combine(_directory, "planta.dwg");
        File.WriteAllBytes(_drawingPath, [1, 2, 3, 4, 5]);
        using var stream = File.OpenRead(_drawingPath);
        _drawingHash = Convert.ToHexString(SHA256.HashData(stream));
        _projectId = Guid.NewGuid();
        _datasetId = Guid.NewGuid();
        _repository = new InMemoryDecisionRepository();
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
    public void Build_CreatesBeforeAfterGroupsWithoutChangingDrawing()
    {
        var before = File.ReadAllBytes(_drawingPath);
        var service = new AutomationPreviewService(_repository);

        var plan = service.Build(
            _projectId,
            CreateSemanticSnapshot(),
            CreateCadSnapshot());

        Assert.AreEqual(8, plan.Groups.Count);
        Assert.IsTrue(plan.SourceIntegrityConfirmed);
        Assert.IsFalse(plan.IsReadyForSupervisedExecution);
        Assert.IsTrue(plan.PendingCount > 0);
        Assert.AreEqual(
            AutomationPreviewDecisionStatus.Protected,
            plan.Groups.Single(
                group => group.Id == "original-architecture").Decision);
        Assert.AreEqual(
            2,
            plan.Groups.Single(
                group => group.Id == "proposed-mask").ItemCount);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(_drawingPath));
    }

    [TestMethod]
    public void Decide_PersistsAuditAndReloadsDecision()
    {
        var service = new AutomationPreviewService(_repository);
        var snapshot = CreateSemanticSnapshot();
        var cad = CreateCadSnapshot();
        var plan = service.Build(_projectId, snapshot, cad);

        var decided = service.Decide(
            plan,
            "detected-electrical",
            AutomationPreviewDecisionStatus.Approved,
            "Conferido na planta.");
        var reloaded = service.Build(_projectId, snapshot, cad);

        var group = decided.Groups.Single(
            item => item.Id == "detected-electrical");
        Assert.AreEqual(
            AutomationPreviewDecisionStatus.Approved,
            group.Decision);
        Assert.AreEqual("Conferido na planta.", group.DecisionNote);
        Assert.AreEqual(
            AutomationPreviewDecisionStatus.Approved,
            reloaded.Groups.Single(
                item => item.Id == "detected-electrical").Decision);
        Assert.AreEqual(1, _repository.Items.Count);
    }

    [TestMethod]
    public void Decide_RejectedGroupNeverMarksPlanReady()
    {
        var service = new AutomationPreviewService(_repository);
        var plan = service.Build(
            _projectId,
            CreateSemanticSnapshot(),
            CreateCadSnapshot());

        var rejected = service.Decide(
            plan,
            "proposed-mask",
            AutomationPreviewDecisionStatus.Rejected,
            null);

        Assert.AreEqual(1, rejected.RejectedCount);
        Assert.IsFalse(rejected.IsReadyForSupervisedExecution);
    }

    private SemanticWorkspaceSnapshot CreateSemanticSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var points = new[]
        {
            CreatePoint(
                "P-EL-1",
                SemanticDiscipline.Electrical,
                SemanticReviewStatus.Approved,
                10,
                20),
            CreatePoint(
                "P-HI-1",
                SemanticDiscipline.Hydraulic,
                SemanticReviewStatus.Corrected,
                30,
                40)
        };
        var dataset = new SemanticDataset(
            _datasetId,
            _projectId,
            Path.GetFileName(_drawingPath),
            _drawingPath,
            "v081",
            new string('A', 64),
            ["pontos.csv", "componentes.csv"],
            2,
            1,
            1,
            0,
            2,
            0,
            0,
            0,
            0,
            true,
            now,
            now);
        return new SemanticWorkspaceSnapshot(
            dataset,
            points,
            [],
            [],
            [],
            []);
    }

    private SemanticPoint CreatePoint(
        string externalId,
        SemanticDiscipline discipline,
        SemanticReviewStatus review,
        double x,
        double y) =>
        new(
            Guid.NewGuid(),
            _datasetId,
            externalId,
            externalId,
            discipline,
            discipline == SemanticDiscipline.Electrical ? "TUG" : "AF",
            "Ponto de teste",
            30,
            "30",
            SemanticConfidence.High,
            "v07",
            discipline == SemanticDiscipline.Electrical
                ? "PONTOS_ELETRICOS"
                : "PONTOS_HIDRAULICOS",
            "BLOCO",
            x,
            y,
            0,
            x,
            y,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            string.Empty,
            4,
            "cm",
            review,
            null,
            null);

    private CadVisualizationSnapshot CreateCadSnapshot() =>
        new(
            _projectId,
            _drawingPath,
            _drawingHash,
            "Teste",
            "1.0",
            DateTimeOffset.UtcNow,
            new CadDrawingBounds(0, 0, 100, 100),
            [new CadLayerInfo("ARQ-PAREDES", 7, true)],
            [
                new CadPrimitive(
                    CadPrimitiveKind.Line,
                    "1",
                    "ARQ-PAREDES",
                    [new CadPoint2D(0, 0), new CadPoint2D(100, 0)])
            ],
            new CadVisualizationCoverage(1, 1, 0),
            Path.Combine(_directory, "preview.json"),
            true);

    private sealed class InMemoryDecisionRepository :
        IAutomationPreviewDecisionRepository
    {
        public List<AutomationPreviewDecision> Items { get; } = [];

        public IReadOnlyList<AutomationPreviewDecision> GetForPlan(
            string planId) =>
            Items.Where(item => item.PlanId == planId).ToArray();

        public void Save(AutomationPreviewDecision decision)
        {
            Items.RemoveAll(
                item => item.PlanId == decision.PlanId
                        && item.GroupId == decision.GroupId);
            Items.Add(decision);
        }
    }
}
