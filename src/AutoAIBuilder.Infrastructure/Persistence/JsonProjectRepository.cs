using System.Text.Json;
using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class JsonProjectRepository : IProjectRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly object _sync = new();
    private bool _isCorrupted;

    public JsonProjectRepository(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("O caminho do repositório é obrigatório.", nameof(filePath));
        }

        _filePath = Path.GetFullPath(filePath);
    }

    public static JsonProjectRepository CreateDefault()
        => new(AppStoragePaths.ProjectsFile);

    public IReadOnlyList<ProjectWorkspace> GetAll()
    {
        lock (_sync)
        {
            return Load().ToArray();
        }
    }

    public ProjectWorkspace? GetById(Guid id)
    {
        lock (_sync)
        {
            return Load().FirstOrDefault(project => project.Id == id);
        }
    }

    public void Save(ProjectWorkspace project)
    {
        ArgumentNullException.ThrowIfNull(project);

        lock (_sync)
        {
            var projects = Load();
            if (_isCorrupted)
            {
                throw new InvalidDataException(
                    "O arquivo de projetos está corrompido e foi preservado sem alterações.");
            }

            var index = projects.FindIndex(item => item.Id == project.Id);

            if (index >= 0)
            {
                projects[index] = project;
            }
            else
            {
                projects.Add(project);
            }

            var directory = Path.GetDirectoryName(_filePath)
                ?? throw new InvalidOperationException("Não foi possível determinar a pasta de dados.");

            Directory.CreateDirectory(directory);

            var temporaryPath = $"{_filePath}.tmp";
            var json = JsonSerializer.Serialize(projects, SerializerOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _filePath, true);
        }
    }

    private List<ProjectWorkspace> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var projects =
                JsonSerializer.Deserialize<List<ProjectWorkspace>>(json, SerializerOptions) ?? [];
            _isCorrupted = false;
            return projects;
        }
        catch (JsonException)
        {
            _isCorrupted = true;
            return [];
        }
    }
}
