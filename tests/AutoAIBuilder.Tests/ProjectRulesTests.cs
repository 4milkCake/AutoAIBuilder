using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class ProjectRulesTests
{
    [TestMethod]
    public void UpdateRules_NormalizesAndStoresTechnicalCriteria()
    {
        var project = ProjectWorkspace.Create(
            "Edifício teste",
            "Residencial",
            4,
            16);

        var updated = project.UpdateRules(new ProjectRules
        {
            MeasurementUnit = "  Metros ",
            DrawingScale = " 1:75 ",
            DefaultFloorHeightMeters = 3.10m,
            NamingStandard = " DISCIPLINA-NÍVEL ",
            RequireLayerStandard = false,
            RequireFileIntegrity = true,
            BlockAutomationOnValidationErrors = true
        });

        Assert.AreEqual("Metros", updated.Rules.MeasurementUnit);
        Assert.AreEqual("1:75", updated.Rules.DrawingScale);
        Assert.AreEqual(3.10m, updated.Rules.DefaultFloorHeightMeters);
        Assert.AreEqual("DISCIPLINA-NÍVEL", updated.Rules.NamingStandard);
        Assert.IsFalse(updated.Rules.RequireLayerStandard);
        Assert.IsTrue(updated.UpdatedAt >= project.UpdatedAt);
    }

    [TestMethod]
    public void UpdateRules_RejectsInvalidFloorHeight()
    {
        var project = ProjectWorkspace.Create(
            "Edifício teste",
            "Residencial",
            4,
            16);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            project.UpdateRules(new ProjectRules
            {
                DefaultFloorHeightMeters = 0
            }));
    }

    [TestMethod]
    public void ArchivedProject_CannotChangeRules()
    {
        var project = ProjectWorkspace.Create(
            "Edifício teste",
            "Residencial",
            4,
            16).Archive();

        Assert.ThrowsException<InvalidOperationException>(() =>
            project.UpdateRules(ProjectRules.CreateDefault()));
    }
}
