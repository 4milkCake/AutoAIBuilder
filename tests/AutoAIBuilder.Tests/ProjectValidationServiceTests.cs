using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Application.Validation;
using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class ProjectValidationServiceTests
{
    private readonly ProjectValidationService _service = new();

    [TestMethod]
    public void Validate_EmptyCatalogBlocksAutomation()
    {
        var project = CreateProject();

        var report = _service.Validate(project, []);

        Assert.AreEqual(1, report.ErrorCount);
        Assert.AreEqual(3, report.PassedCount);
        Assert.IsTrue(report.IsAutomationBlocked);
        Assert.AreEqual(
            "FILE_CATALOG",
            report.Results.Single(result => result.Status == ProjectValidationStatus.Error).Code);
    }

    [TestMethod]
    public void Validate_HealthyDrawingApprovesAllChecks()
    {
        var project = AddDrawing(CreateProject());
        var drawing = project.Files.Single();
        ProjectFileInspection[] inspections =
        [
            new(drawing, true, false, drawing.SizeBytes, drawing.LastKnownWriteTime)
        ];

        var report = _service.Validate(project, inspections);

        Assert.AreEqual(4, report.PassedCount);
        Assert.AreEqual(0, report.WarningCount);
        Assert.AreEqual(0, report.ErrorCount);
        Assert.IsFalse(report.IsAutomationBlocked);
    }

    [TestMethod]
    public void Validate_ChangedDrawingProducesWarning()
    {
        var project = AddDrawing(CreateProject());
        var drawing = project.Files.Single();
        ProjectFileInspection[] inspections =
        [
            new(drawing, true, true, drawing.SizeBytes + 10, DateTimeOffset.Now)
        ];

        var report = _service.Validate(project, inspections);

        Assert.AreEqual(1, report.WarningCount);
        Assert.AreEqual(0, report.ErrorCount);
        Assert.AreEqual(
            ProjectValidationStatus.Warning,
            report.Results.Single(result => result.Code == "FILE_CATALOG").Status);
    }

    [TestMethod]
    public void Validate_MissingRequiredFileProducesBlockingError()
    {
        var project = AddDrawing(CreateProject());
        var drawing = project.Files.Single();
        ProjectFileInspection[] inspections =
        [
            new(drawing, false, false, null, null)
        ];

        var report = _service.Validate(project, inspections);

        Assert.AreEqual(1, report.ErrorCount);
        Assert.IsTrue(report.IsAutomationBlocked);
    }

    [TestMethod]
    public void Validate_ErrorDoesNotBlockWhenProjectRuleAllowsIt()
    {
        var project = CreateProject().UpdateRules(new ProjectRules
        {
            BlockAutomationOnValidationErrors = false
        });

        var report = _service.Validate(project, []);

        Assert.AreEqual(1, report.ErrorCount);
        Assert.IsFalse(report.IsAutomationBlocked);
    }

    [TestMethod]
    public void Validate_MissingStandardLayerProducesError()
    {
        var project = AddDrawing(CreateProject());
        project = project with { Layers = project.Layers.Skip(1).ToArray() };
        var drawing = project.Files.Single();

        var report = _service.Validate(
            project,
            [new(drawing, true, false, drawing.SizeBytes, drawing.LastKnownWriteTime)]);

        var layerResult = report.Results.Single(result => result.Code == "LAYER_STANDARD");
        Assert.AreEqual(ProjectValidationStatus.Error, layerResult.Status);
        Assert.IsTrue(layerResult.Detail.Contains("ARQ-PAREDES", StringComparison.Ordinal));
    }

    private static ProjectWorkspace CreateProject() =>
        ProjectWorkspace.Create(
            "Edifício validado",
            "Residencial",
            8,
            32);

    private static ProjectWorkspace AddDrawing(ProjectWorkspace project) =>
        project.RegisterFile(
            Path.Combine(Path.GetTempPath(), "planta-validacao.dwg"),
            1_024,
            lastKnownWriteTime: DateTimeOffset.Now);
}
