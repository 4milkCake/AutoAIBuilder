using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Application.Projects;

public interface IProjectRepository
{
    IReadOnlyList<ProjectWorkspace> GetAll();
    ProjectWorkspace? GetById(Guid id);
    void Save(ProjectWorkspace project);
}
