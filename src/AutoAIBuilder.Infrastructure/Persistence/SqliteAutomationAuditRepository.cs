using System.Globalization;
using System.Text.Json;
using AutoAIBuilder.Application.Automation.Execution;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class SqliteAutomationAuditRepository(
    SqliteDatabase database) : IAutomationAuditRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AutomationAuditEntry? Get(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = SelectColumns
                                  + " WHERE Id = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadEntry(reader) : null;
        }
    }

    public AutomationAuditEntry? GetLatestByPlanId(Guid planId)
    {
        if (planId == Guid.Empty)
        {
            return null;
        }

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                SelectColumns
                + """
                   WHERE PlanId = $planId
                   ORDER BY CreatedAt DESC
                   LIMIT 1;
                  """;
            command.Parameters.AddWithValue("$planId", planId.ToString("D"));
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadEntry(reader) : null;
        }
    }

    public AutomationAuditEntry? GetSuccessfulByIdempotencyKey(
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                SelectColumns
                + """
                   WHERE IdempotencyKey = $idempotencyKey
                     AND Status = $succeeded
                   ORDER BY CompletedAt DESC
                   LIMIT 1;
                  """;
            command.Parameters.AddWithValue(
                "$idempotencyKey",
                idempotencyKey.Trim());
            command.Parameters.AddWithValue(
                "$succeeded",
                (int)AutomationAuditStatus.Succeeded);
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadEntry(reader) : null;
        }
    }

    public IReadOnlyList<AutomationAuditEntry> GetRecent(
        int maximumEntries = 50)
    {
        if (maximumEntries is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumEntries),
                "O limite deve estar entre 1 e 500.");
        }

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                SelectColumns
                + " ORDER BY CreatedAt DESC LIMIT $maximumEntries;";
            command.Parameters.AddWithValue("$maximumEntries", maximumEntries);
            var entries = new List<AutomationAuditEntry>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                entries.Add(ReadEntry(reader));
            }

            return entries;
        }
    }

    public void Save(AutomationAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Validate(entry);

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO AutomationAudits (
                    Id,
                    PlanId,
                    ProjectId,
                    MaskId,
                    MaskVersion,
                    Mode,
                    Status,
                    IdempotencyKey,
                    InputsJson,
                    OutputsJson,
                    PublishedPath,
                    RecoveryPath,
                    Summary,
                    CreatedAt,
                    CompletedAt,
                    UpdatedAt)
                VALUES (
                    $id,
                    $planId,
                    $projectId,
                    $maskId,
                    $maskVersion,
                    $mode,
                    $status,
                    $idempotencyKey,
                    $inputsJson,
                    $outputsJson,
                    $publishedPath,
                    $recoveryPath,
                    $summary,
                    $createdAt,
                    $completedAt,
                    $updatedAt)
                ON CONFLICT(Id) DO UPDATE SET
                    PlanId = excluded.PlanId,
                    ProjectId = excluded.ProjectId,
                    MaskId = excluded.MaskId,
                    MaskVersion = excluded.MaskVersion,
                    Mode = excluded.Mode,
                    Status = excluded.Status,
                    IdempotencyKey = excluded.IdempotencyKey,
                    InputsJson = excluded.InputsJson,
                    OutputsJson = excluded.OutputsJson,
                    PublishedPath = excluded.PublishedPath,
                    RecoveryPath = excluded.RecoveryPath,
                    Summary = excluded.Summary,
                    CreatedAt = excluded.CreatedAt,
                    CompletedAt = excluded.CompletedAt,
                    UpdatedAt = excluded.UpdatedAt;
                """;
            command.Parameters.AddWithValue("$id", entry.Id.ToString("D"));
            command.Parameters.AddWithValue("$planId", entry.PlanId.ToString("D"));
            command.Parameters.AddWithValue(
                "$projectId",
                entry.ProjectId.ToString("D"));
            command.Parameters.AddWithValue("$maskId", entry.MaskId);
            command.Parameters.AddWithValue("$maskVersion", entry.MaskVersion);
            command.Parameters.AddWithValue("$mode", (int)entry.Mode);
            command.Parameters.AddWithValue("$status", (int)entry.Status);
            command.Parameters.AddWithValue(
                "$idempotencyKey",
                entry.IdempotencyKey);
            command.Parameters.AddWithValue(
                "$inputsJson",
                JsonSerializer.Serialize(entry.Inputs, SerializerOptions));
            command.Parameters.AddWithValue(
                "$outputsJson",
                JsonSerializer.Serialize(entry.OutputPaths, SerializerOptions));
            command.Parameters.AddWithValue(
                "$publishedPath",
                entry.PublishedPath ?? (object)DBNull.Value);
            command.Parameters.AddWithValue(
                "$recoveryPath",
                entry.RecoveryPath ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$summary", entry.Summary);
            command.Parameters.AddWithValue(
                "$createdAt",
                entry.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue(
                "$completedAt",
                entry.CompletedAt?.ToString("O") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue(
                "$updatedAt",
                DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    public int MarkIncompleteAsInterrupted(
        DateTimeOffset interruptedAt,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "O motivo da interrupção é obrigatório.",
                nameof(reason));
        }

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE AutomationAudits
                SET
                    Status = $interrupted,
                    Summary = $summary,
                    CompletedAt = $completedAt,
                    UpdatedAt = $completedAt
                WHERE Status IN ($planned, $running);
                """;
            command.Parameters.AddWithValue(
                "$interrupted",
                (int)AutomationAuditStatus.Interrupted);
            command.Parameters.AddWithValue("$summary", reason.Trim());
            command.Parameters.AddWithValue(
                "$completedAt",
                interruptedAt.ToString("O"));
            command.Parameters.AddWithValue(
                "$planned",
                (int)AutomationAuditStatus.Planned);
            command.Parameters.AddWithValue(
                "$running",
                (int)AutomationAuditStatus.Running);
            var affected = command.ExecuteNonQuery();
            transaction.Commit();
            return affected;
        }
    }

    private static AutomationAuditEntry ReadEntry(SqliteDataReader reader)
    {
        var modeValue = reader.GetInt32(5);
        var statusValue = reader.GetInt32(6);
        if (!Enum.IsDefined(typeof(AutomationExecutionMode), modeValue)
            || !Enum.IsDefined(typeof(AutomationAuditStatus), statusValue))
        {
            throw new InvalidDataException(
                "A auditoria contém modo ou estado desconhecido.");
        }

        try
        {
            var entry = new AutomationAuditEntry(
                ParseGuid(reader.GetString(0), "auditoria"),
                ParseGuid(reader.GetString(1), "plano"),
                ParseGuid(reader.GetString(2), "projeto"),
                reader.GetString(3),
                reader.GetString(4),
                (AutomationExecutionMode)modeValue,
                (AutomationAuditStatus)statusValue,
                reader.GetString(7),
                JsonSerializer.Deserialize<List<AutomationInputSnapshot>>(
                    reader.GetString(8),
                    SerializerOptions) ?? [],
                JsonSerializer.Deserialize<List<string>>(
                    reader.GetString(9),
                    SerializerOptions) ?? [],
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.GetString(12),
                ParseTimestamp(reader.GetString(13)),
                reader.IsDBNull(14)
                    ? null
                    : ParseTimestamp(reader.GetString(14)));
            Validate(entry);
            return entry;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "As evidências JSON da auditoria estão inválidas.",
                exception);
        }
    }

    private static void Validate(AutomationAuditEntry entry)
    {
        if (entry.Id == Guid.Empty
            || entry.PlanId == Guid.Empty
            || entry.ProjectId == Guid.Empty)
        {
            throw new InvalidDataException(
                "A auditoria contém identificadores obrigatórios inválidos.");
        }

        if (string.IsNullOrWhiteSpace(entry.MaskId)
            || string.IsNullOrWhiteSpace(entry.MaskVersion)
            || string.IsNullOrWhiteSpace(entry.IdempotencyKey)
            || string.IsNullOrWhiteSpace(entry.Summary)
            || !Enum.IsDefined(entry.Mode)
            || !Enum.IsDefined(entry.Status))
        {
            throw new InvalidDataException(
                "A auditoria contém campos obrigatórios inválidos.");
        }
    }

    private static Guid ParseGuid(string value, string fieldName) =>
        Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidDataException(
                $"O identificador de {fieldName} persistido é inválido.");

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : throw new InvalidDataException(
                "Uma data de auditoria persistida é inválida.");

    private const string SelectColumns =
        """
        SELECT
            Id,
            PlanId,
            ProjectId,
            MaskId,
            MaskVersion,
            Mode,
            Status,
            IdempotencyKey,
            InputsJson,
            OutputsJson,
            PublishedPath,
            RecoveryPath,
            Summary,
            CreatedAt,
            CompletedAt
        FROM AutomationAudits
        """;
}
