using AutoAIBuilder.Application.History;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class SqliteActivityLogRepository : IActivityLogRepository
{
    public const int MaximumEntries = 500;

    private readonly SqliteDatabase _database;

    public SqliteActivityLogRepository(SqliteDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _database.Initialize();
    }

    public IReadOnlyList<ActivityLogEntry> GetAll()
    {
        lock (_database.SyncRoot)
        {
            using var connection = _database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT Id, OccurredAt, Category, Action, Description, Level,
                       ProjectId, ProjectName
                FROM ActivityLog
                ORDER BY OccurredAt ASC;
                """;
            using var reader = command.ExecuteReader();

            var entries = new List<ActivityLogEntry>();
            while (reader.Read())
            {
                entries.Add(new ActivityLogEntry(
                    Guid.Parse(reader.GetString(0)),
                    DateTimeOffset.Parse(
                        reader.GetString(1),
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    (ActivityLevel)reader.GetInt32(5),
                    reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
                    reader.IsDBNull(7) ? null : reader.GetString(7)));
            }

            return entries;
        }
    }

    public void Append(ActivityLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_database.SyncRoot)
        {
            using var connection = _database.OpenConnection();
            using var transaction = connection.BeginTransaction();

            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText =
                    """
                    INSERT INTO ActivityLog (
                        Id, OccurredAt, Category, Action, Description, Level,
                        ProjectId, ProjectName)
                    VALUES (
                        $id, $occurredAt, $category, $action, $description, $level,
                        $projectId, $projectName)
                    ON CONFLICT(Id) DO UPDATE SET
                        OccurredAt = excluded.OccurredAt,
                        Category = excluded.Category,
                        Action = excluded.Action,
                        Description = excluded.Description,
                        Level = excluded.Level,
                        ProjectId = excluded.ProjectId,
                        ProjectName = excluded.ProjectName;
                    """;
                insert.Parameters.AddWithValue("$id", entry.Id.ToString("D"));
                insert.Parameters.AddWithValue(
                    "$occurredAt",
                    entry.OccurredAt.UtcDateTime.ToString("O"));
                insert.Parameters.AddWithValue("$category", entry.Category);
                insert.Parameters.AddWithValue("$action", entry.Action);
                insert.Parameters.AddWithValue("$description", entry.Description);
                insert.Parameters.AddWithValue("$level", (int)entry.Level);
                insert.Parameters.AddWithValue(
                    "$projectId",
                    entry.ProjectId?.ToString("D") ?? (object)DBNull.Value);
                insert.Parameters.AddWithValue(
                    "$projectName",
                    entry.ProjectName ?? (object)DBNull.Value);
                insert.ExecuteNonQuery();
            }

            using (var trim = connection.CreateCommand())
            {
                trim.Transaction = transaction;
                trim.CommandText =
                    """
                    DELETE FROM ActivityLog
                    WHERE Id IN (
                        SELECT Id
                        FROM ActivityLog
                        ORDER BY OccurredAt DESC, rowid DESC
                        LIMIT -1 OFFSET $maximumEntries
                    );
                    """;
                trim.Parameters.AddWithValue("$maximumEntries", MaximumEntries);
                trim.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }
}
