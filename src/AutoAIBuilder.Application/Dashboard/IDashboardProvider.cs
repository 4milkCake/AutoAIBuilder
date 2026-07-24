using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Application.Dashboard;

public interface IDashboardProvider
{
    DashboardSnapshot GetFor(ProjectWorkspace project);
}
