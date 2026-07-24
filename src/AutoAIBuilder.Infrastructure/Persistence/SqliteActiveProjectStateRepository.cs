using AutoAIBuilder.Application.Projects;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class SqliteActiveProjectStateRepository
    : IActiveProjectStateRepository
{
    private const string StateKey = "active-project-id";
    private readonly SqliteDatabase _database;

    public SqliteActiveProjectStateRepository(SqliteDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _database.Initialize();
    }

    public Guid? Load()
    {
        lock (_database.SyncRoot)
        {
            using var connection = _database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Value FROM AppState WHERE Key = $key LIMIT 1;";
            command.Parameters.AddWithValue("$key", StateKey);
            var value = command.ExecuteScalar() as string;
            return Guid.TryParse(value, out var projectId) ? projectId : null;
        }
    }

    public void Save(Guid? projectId)
    {
        lock (_database.SyncRoot)
        {
            using var connection = _database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            if (projectId is null)
            {
                command.CommandText = "DELETE FROM AppState WHERE Key = $key;";
                command.Parameters.AddWithValue("$key", StateKey);
            }
            else
            {
                command.CommandText =
                    """
                    INSERT INTO AppState (Key, Value, UpdatedAt)
                    VALUES ($key, $value, $updatedAt)
                    ON CONFLICT(Key) DO UPDATE SET
                        Value = excluded.Value,
                        UpdatedAt = excluded.UpdatedAt;
                    """;
                command.Parameters.AddWithValue("$key", StateKey);
                command.Parameters.AddWithValue(
                    "$value",
                    projectId.Value.ToString("D"));
                command.Parameters.AddWithValue(
                    "$updatedAt",
                    DateTimeOffset.UtcNow.ToString("O"));
            }

            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }
}
