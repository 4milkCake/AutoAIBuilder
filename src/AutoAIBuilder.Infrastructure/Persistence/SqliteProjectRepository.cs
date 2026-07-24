using System.Text.Json;
using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class SqliteProjectRepository : IProjectRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly SqliteDatabase _database;
    private bool _isCorrupted;

    public SqliteProjectRepository(SqliteDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _database.Initialize();
    }

    public IReadOnlyList<ProjectWorkspace> GetAll()
    {
        lock (_database.SyncRoot)
        {
            using var connection = _database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT PayloadJson FROM Projects ORDER BY UpdatedAt DESC;";
            using var reader = command.ExecuteReader();

            var projects = new List<ProjectWorkspace>();
            var corrupted = false;
            while (reader.Read())
            {
                try
                {
                    projects.Add(Deserialize(reader.GetString(0)));
                }
                catch (InvalidDataException)
                {
                    corrupted = true;
                }
            }

            _isCorrupted = corrupted;
            return projects;
        }
    }

    public ProjectWorkspace? GetById(Guid id)
    {
        lock (_database.SyncRoot)
        {
            using var connection = _database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT PayloadJson FROM Projects WHERE Id = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            var payload = command.ExecuteScalar() as string;
            if (payload is null)
            {
                return null;
            }

            try
            {
                return Deserialize(payload);
            }
            catch (InvalidDataException)
            {
                _isCorrupted = true;
                return null;
            }
        }
    }

    public void Save(ProjectWorkspace project)
    {
        ArgumentNullException.ThrowIfNull(project);

        lock (_database.SyncRoot)
        {
            if (_isCorrupted)
            {
                throw new InvalidDataException(
                    "Existem projetos corrompidos no banco. O conteúdo foi "
                    + "preservado e novas gravações estão bloqueadas.");
            }

            using var connection = _database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO Projects (Id, PayloadJson, UpdatedAt)
                VALUES ($id, $payload, $updatedAt)
                ON CONFLICT(Id) DO UPDATE SET
                    PayloadJson = excluded.PayloadJson,
                    UpdatedAt = excluded.UpdatedAt;
                """;
            command.Parameters.AddWithValue("$id", project.Id.ToString("D"));
            command.Parameters.AddWithValue(
                "$payload",
                JsonSerializer.Serialize(project, SerializerOptions));
            command.Parameters.AddWithValue(
                "$updatedAt",
                project.UpdatedAt.UtcDateTime.ToString("O"));
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    private static ProjectWorkspace Deserialize(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<ProjectWorkspace>(
                       payload,
                       SerializerOptions)
                   ?? throw new InvalidDataException(
                       "Um projeto armazenado no banco não possui conteúdo válido.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Um projeto armazenado no banco está corrompido e não foi alterado.",
                exception);
        }
    }
}
