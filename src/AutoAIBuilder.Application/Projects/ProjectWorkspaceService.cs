using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Application.Projects;

public sealed class ProjectWorkspaceService(IProjectRepository repository)
{
    public IReadOnlyList<ProjectWorkspace> GetProjects() =>
        repository.GetAll()
            .OrderByDescending(project => project.UpdatedAt)
            .ToArray();

    public IReadOnlyList<ProjectWorkspace> GetActiveProjects() =>
        GetProjects()
            .Where(project => !project.IsArchived)
            .ToArray();

    public IReadOnlyList<ProjectWorkspace> GetArchivedProjects() =>
        GetProjects()
            .Where(project => project.IsArchived)
            .ToArray();

    public ProjectWorkspace? GetProject(Guid projectId) =>
        repository.GetById(projectId);

    public ProjectWorkspace CreateProject(CreateProjectRequest request)
    {
        var project = ProjectWorkspace.Create(
            request.Name,
            request.Type,
            request.Floors,
            request.Units);

        repository.Save(project);
        return project;
    }

    public ProjectWorkspace UpdateProject(UpdateProjectRequest request)
    {
        var project = RequireProject(request.ProjectId);
        var updatedProject = project.UpdateDetails(
            request.Name,
            request.Type,
            request.Floors,
            request.Units);

        repository.Save(updatedProject);
        return updatedProject;
    }

    public ProjectWorkspace UpdateProjectRules(Guid projectId, ProjectRules rules)
    {
        var updatedProject = RequireProject(projectId).UpdateRules(rules);
        repository.Save(updatedProject);
        return updatedProject;
    }

    public ProjectWorkspace DuplicateProject(Guid projectId, string? name = null)
    {
        var project = RequireProject(projectId);
        var duplicate = project.Duplicate(name);
        repository.Save(duplicate);
        return duplicate;
    }

    public ProjectWorkspace ArchiveProject(Guid projectId)
    {
        var archivedProject = RequireProject(projectId).Archive();
        repository.Save(archivedProject);
        return archivedProject;
    }

    public ProjectWorkspace RestoreProject(Guid projectId)
    {
        var restoredProject = RequireProject(projectId).Restore();
        repository.Save(restoredProject);
        return restoredProject;
    }

    public ProjectWorkspace RegisterFile(Guid projectId, string sourcePath)
    {
        return RegisterFiles(projectId, [sourcePath]);
    }

    public ProjectWorkspace RegisterFiles(
        Guid projectId,
        IEnumerable<string> sourcePaths)
    {
        var project = RequireProject(projectId);

        if (project.IsArchived)
        {
            throw new InvalidOperationException("Restaure o projeto antes de catalogar arquivos.");
        }

        var updatedProject = project;

        foreach (var sourcePath in sourcePaths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("O arquivo selecionado não existe.", sourcePath);
            }

            var file = new FileInfo(sourcePath);
            updatedProject = updatedProject.RegisterFile(
                file.FullName,
                file.Length,
                lastKnownWriteTime: ToDateTimeOffset(file.LastWriteTimeUtc));
        }

        repository.Save(updatedProject);
        return updatedProject;
    }

    public ProjectWorkspace RemoveFileReference(Guid projectId, Guid fileId)
    {
        var updatedProject = RequireProject(projectId).RemoveFileReference(fileId);
        repository.Save(updatedProject);
        return updatedProject;
    }

    public ProjectWorkspace RefreshFileMetadata(Guid projectId, Guid fileId)
    {
        var project = RequireProject(projectId);
        var projectFile = project.Files.FirstOrDefault(file => file.Id == fileId)
            ?? throw new InvalidOperationException("A referência de arquivo não foi encontrada.");

        if (!File.Exists(projectFile.SourcePath))
        {
            throw new FileNotFoundException(
                "O arquivo original não está disponível no caminho catalogado.",
                projectFile.SourcePath);
        }

        var file = new FileInfo(projectFile.SourcePath);
        var updatedProject = project.UpdateFileMetadata(
            fileId,
            file.Length,
            ToDateTimeOffset(file.LastWriteTimeUtc));

        repository.Save(updatedProject);
        return updatedProject;
    }

    public IReadOnlyList<ProjectFileInspection> InspectFiles(Guid projectId)
    {
        var project = RequireProject(projectId);
        return project.Files
            .Select(InspectFile)
            .ToArray();
    }

    private ProjectWorkspace RequireProject(Guid projectId) =>
        repository.GetById(projectId)
        ?? throw new InvalidOperationException("O projeto selecionado não foi encontrado.");

    private static ProjectFileInspection InspectFile(ProjectFile projectFile)
    {
        try
        {
            if (!File.Exists(projectFile.SourcePath))
            {
                return new ProjectFileInspection(projectFile, false, false, null, null);
            }

            var file = new FileInfo(projectFile.SourcePath);
            var lastWriteTime = ToDateTimeOffset(file.LastWriteTimeUtc);
            var hasChanged = file.Length != projectFile.SizeBytes
                || projectFile.LastKnownWriteTime is not null
                && lastWriteTime != projectFile.LastKnownWriteTime;

            return new ProjectFileInspection(
                projectFile,
                true,
                hasChanged,
                file.Length,
                lastWriteTime);
        }
        catch (UnauthorizedAccessException)
        {
            return new ProjectFileInspection(projectFile, false, false, null, null);
        }
        catch (IOException)
        {
            return new ProjectFileInspection(projectFile, false, false, null, null);
        }
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
