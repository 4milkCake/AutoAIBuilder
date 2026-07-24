using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Application.Reports;
using AutoAIBuilder.Application.Validation;
using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class ProjectReportServiceTests
{
    [TestMethod]
    public void Build_IncludesProjectRulesFilesAndValidationSummary()
    {
        var project = ProjectWorkspace.Create(
            "Edifício Central",
            "Residencial",
            8,
            32).RegisterFile(
                Path.Combine(Path.GetTempPath(), "planta-base.dwg"),
                4_096,
                lastKnownWriteTime: DateTimeOffset.Now);
        var file = project.Files.Single();
        ProjectFileInspection[] inspections =
        [
            new(file, true, false, file.SizeBytes, file.LastKnownWriteTime)
        ];
        var validation = new ProjectValidationService().Validate(project, inspections);
        var generatedAt = new DateTimeOffset(
            2026,
            7,
            24,
            14,
            30,
            0,
            TimeSpan.FromHours(-3));

        var report = new ProjectReportService().Build(
            project,
            inspections,
            validation,
            generatedAt);

        StringAssert.Contains(report.Content, "Edifício Central");
        StringAssert.Contains(report.Content, "planta-base.dwg");
        StringAssert.Contains(report.Content, "REGRAS TÉCNICAS");
        StringAssert.Contains(report.Content, "Aprovados: 4");
        StringAssert.Contains(report.CsvContent, "\"PROJECT_DATA\"");
        StringAssert.Contains(report.CsvContent, "\"APROVADO\"");
        Assert.AreEqual(0, report.ErrorCount);
        Assert.IsFalse(report.IsAutomationBlocked);
        Assert.IsTrue(report.SuggestedFileName.EndsWith(".txt", StringComparison.Ordinal));
        Assert.IsTrue(report.SuggestedCsvFileName.EndsWith(".csv", StringComparison.Ordinal));
        Assert.IsTrue(report.SuggestedPdfFileName.EndsWith(".pdf", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Build_RejectsValidationFromAnotherProject()
    {
        var project = ProjectWorkspace.Create("Projeto A", "Residencial", 1, 1);
        var otherProject = ProjectWorkspace.Create("Projeto B", "Residencial", 1, 1);
        var validation = new ProjectValidationService().Validate(otherProject, []);

        Assert.ThrowsException<ArgumentException>(() =>
            new ProjectReportService().Build(project, [], validation));
    }
}
