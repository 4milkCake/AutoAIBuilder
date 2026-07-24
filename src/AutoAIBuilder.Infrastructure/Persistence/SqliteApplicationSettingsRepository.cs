using System.Text.Json;
using AutoAIBuilder.Application.Settings;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class SqliteApplicationSettingsRepository
    : IApplicationSettingsRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly SqliteDatabase _database;
    private bool _isCorrupted;

    public SqliteApplicationSettingsRepository(SqliteDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _database.Initialize();
    }

    public ApplicationSettings Load()
    {
        lock (_database.SyncRoot)
        {
            using var connection = _database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT PayloadJson FROM ApplicationSettings WHERE Id = 1;";
            var payload = command.ExecuteScalar() as string;

            if (payload is null)
            {
                _isCorrupted = false;
                return ApplicationSettings.CreateDefault();
            }

            try
            {
                var settings = JsonSerializer.Deserialize<ApplicationSettings>(
                           payload,
                           SerializerOptions)
                       ?? ApplicationSettings.CreateDefault();
                _isCorrupted = false;
                return settings;
            }
            catch (JsonException exception)
            {
                _isCorrupted = true;
                throw new InvalidDataException(
                    "As configurações armazenadas no banco estão corrompidas.",
                    exception);
            }
        }
    }

    public void Save(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_database.SyncRoot)
        {
            if (_isCorrupted)
            {
                throw new InvalidDataException(
                    "As configurações armazenadas estão corrompidas e foram "
                    + "preservadas sem sobrescrita.");
            }

            using var connection = _database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO ApplicationSettings (Id, PayloadJson, UpdatedAt)
                VALUES (1, $payload, $updatedAt)
                ON CONFLICT(Id) DO UPDATE SET
                    PayloadJson = excluded.PayloadJson,
                    UpdatedAt = excluded.UpdatedAt;
                """;
            command.Parameters.AddWithValue(
                "$payload",
                JsonSerializer.Serialize(settings, SerializerOptions));
            command.Parameters.AddWithValue(
                "$updatedAt",
                DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }
}
