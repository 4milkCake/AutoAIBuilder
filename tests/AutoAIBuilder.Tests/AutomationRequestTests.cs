using AutoAIBuilder.Application.Automation;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class AutomationRequestTests
{
    [TestMethod]
    public void Create_NormalizesWorkflowWithoutExecutingAnything()
    {
        var projectId = Guid.NewGuid();

        var request = AutomationRequest.Create(projectId, "  fluxo-eletrico  ");

        Assert.AreEqual(projectId, request.ProjectId);
        Assert.AreEqual("fluxo-eletrico", request.WorkflowId);
        Assert.AreEqual(0, request.Parameters.Count);
    }

    [TestMethod]
    public void Create_RejectsInvalidProjectAndWorkflow()
    {
        Assert.ThrowsException<ArgumentException>(
            () => AutomationRequest.Create(Guid.Empty, "fluxo"));
        Assert.ThrowsException<ArgumentException>(
            () => AutomationRequest.Create(Guid.NewGuid(), " "));
    }
}
