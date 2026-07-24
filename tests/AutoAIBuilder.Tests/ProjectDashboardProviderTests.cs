using AutoAIBuilder.Domain.Automation;
using AutoAIBuilder.Domain.Projects;
using AutoAIBuilder.Infrastructure.Dashboard;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class ProjectDashboardProviderTests
{
    [TestMethod]
    public void GetFor_UsesRealProjectDataAndKeepsImportPendingWithoutFiles()
    {
        var project = ProjectWorkspace.Create(
            "Projeto real",
            "Comercial",
            floors: 4,
            units: 12,
            now: new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.FromHours(-3)));

        var snapshot = new ProjectDashboardProvider().GetFor(project);

        Assert.AreEqual(project.Id, snapshot.Project.Id);
        Assert.AreEqual("Projeto real", snapshot.Project.Name);
        Assert.AreEqual(WorkflowState.Pending, snapshot.Workflow[0].State);
        Assert.AreEqual("0", snapshot.Metrics[0].Value);
        StringAssert.Contains(snapshot.RecommendedAction, "primeiro arquivo");
    }

    [TestMethod]
    public void GetFor_MarksImportCompleteWhenProjectHasCatalogedFile()
    {
        var project = ProjectWorkspace.Create(
                "Projeto com arquivo",
                "Residencial",
                floors: 1,
                units: 1)
            .RegisterFile(
                Path.Combine(Path.GetTempPath(), "planta.dwg"),
                sizeBytes: 2048);

        var snapshot = new ProjectDashboardProvider().GetFor(project);

        Assert.AreEqual(WorkflowState.Completed, snapshot.Workflow[0].State);
        Assert.AreEqual("1", snapshot.Metrics[0].Value);
        StringAssert.Contains(snapshot.RecommendedAction, "catálogo");
    }
}
