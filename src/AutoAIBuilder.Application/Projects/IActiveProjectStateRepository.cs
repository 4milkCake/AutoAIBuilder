namespace AutoAIBuilder.Application.Projects;

public interface IActiveProjectStateRepository
{
    Guid? Load();

    void Save(Guid? projectId);
}
