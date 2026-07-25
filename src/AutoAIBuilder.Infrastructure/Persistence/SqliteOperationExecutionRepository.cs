using System.Globalization;
using AutoAIBuilder.Application.Operations;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class SqliteOperationExecutionRepository(
    SqliteDatabase database) : IOperationExecutionRepository
{
    public OperationExecution? Get(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    Id,
                    OperationType,
                    DisplayName,
                    ResourceKey,
                    ProjectId,
                    Status,
                    Progress,
                    CurrentStep,
                    CreatedAt,
                    StartedAt,
                    CompletedAt,
                    TimeoutSeconds,
                    ErrorCode,
                    ErrorMessage
                FROM OperationExecutions
                WHERE Id = $id
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$id", id.ToString());

            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadExecution(reader) : null;
        }
    }

    public IReadOnlyList<OperationExecution> GetRecent(int maximumEntries = 50)
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
                """
                SELECT
                    Id,
                    OperationType,
                    DisplayName,
                    ResourceKey,
                    ProjectId,
                    Status,
                    Progress,
                    CurrentStep,
                    CreatedAt,
                    StartedAt,
                    CompletedAt,
                    TimeoutSeconds,
                    ErrorCode,
                    ErrorMessage
                FROM OperationExecutions
                ORDER BY CreatedAt DESC
                LIMIT $maximumEntries;
                """;
            command.Parameters.AddWithValue("$maximumEntries", maximumEntries);

            var executions = new List<OperationExecution>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                executions.Add(ReadExecution(reader));
            }

            return executions;
        }
    }

    public void Save(OperationExecution execution)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ValidateExecution(execution);

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO OperationExecutions (
                    Id,
                    OperationType,
                    DisplayName,
                    ResourceKey,
                    ProjectId,
                    Status,
                    Progress,
                    CurrentStep,
                    CreatedAt,
                    StartedAt,
                    CompletedAt,
                    TimeoutSeconds,
                    ErrorCode,
                    ErrorMessage,
                    UpdatedAt)
                VALUES (
                    $id,
                    $operationType,
                    $displayName,
                    $resourceKey,
                    $projectId,
                    $status,
                    $progress,
                    $currentStep,
                    $createdAt,
                    $startedAt,
                    $completedAt,
                    $timeoutSeconds,
                    $errorCode,
                    $errorMessage,
                    $updatedAt)
                ON CONFLICT(Id) DO UPDATE SET
                    OperationType = excluded.OperationType,
                    DisplayName = excluded.DisplayName,
                    ResourceKey = excluded.ResourceKey,
                    ProjectId = excluded.ProjectId,
                    Status = excluded.Status,
                    Progress = excluded.Progress,
                    CurrentStep = excluded.CurrentStep,
                    CreatedAt = excluded.CreatedAt,
                    StartedAt = excluded.StartedAt,
                    CompletedAt = excluded.CompletedAt,
                    TimeoutSeconds = excluded.TimeoutSeconds,
                    ErrorCode = excluded.ErrorCode,
                    ErrorMessage = excluded.ErrorMessage,
                    UpdatedAt = excluded.UpdatedAt;
                """;
            command.Parameters.AddWithValue("$id", execution.Id.ToString());
            command.Parameters.AddWithValue(
                "$operationType",
                execution.OperationType);
            command.Parameters.AddWithValue("$displayName", execution.DisplayName);
            command.Parameters.AddWithValue("$resourceKey", execution.ResourceKey);
            command.Parameters.AddWithValue(
                "$projectId",
                execution.ProjectId?.ToString() ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$status", (int)execution.Status);
            command.Parameters.AddWithValue("$progress", execution.Progress);
            command.Parameters.AddWithValue("$currentStep", execution.CurrentStep);
            command.Parameters.AddWithValue(
                "$createdAt",
                execution.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue(
                "$startedAt",
                execution.StartedAt?.ToString("O") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue(
                "$completedAt",
                execution.CompletedAt?.ToString("O") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue(
                "$timeoutSeconds",
                execution.TimeoutSeconds);
            command.Parameters.AddWithValue(
                "$errorCode",
                execution.ErrorCode ?? (object)DBNull.Value);
            command.Parameters.AddWithValue(
                "$errorMessage",
                execution.ErrorMessage ?? (object)DBNull.Value);
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
                UPDATE OperationExecutions
                SET
                    Status = $interruptedStatus,
                    CurrentStep = $currentStep,
                    CompletedAt = $completedAt,
                    ErrorCode = $errorCode,
                    ErrorMessage = $errorMessage,
                    UpdatedAt = $updatedAt
                WHERE Status IN ($pendingStatus, $runningStatus);
                """;
            command.Parameters.AddWithValue(
                "$interruptedStatus",
                (int)OperationExecutionStatus.Interrupted);
            command.Parameters.AddWithValue(
                "$currentStep",
                "Execução interrompida");
            command.Parameters.AddWithValue(
                "$completedAt",
                interruptedAt.ToString("O"));
            command.Parameters.AddWithValue(
                "$errorCode",
                "PROCESS_INTERRUPTED");
            command.Parameters.AddWithValue("$errorMessage", reason.Trim());
            command.Parameters.AddWithValue(
                "$updatedAt",
                interruptedAt.ToString("O"));
            command.Parameters.AddWithValue(
                "$pendingStatus",
                (int)OperationExecutionStatus.Pending);
            command.Parameters.AddWithValue(
                "$runningStatus",
                (int)OperationExecutionStatus.Running);
            var affectedRows = command.ExecuteNonQuery();
            transaction.Commit();
            return affectedRows;
        }
    }

    private static OperationExecution ReadExecution(SqliteDataReader reader)
    {
        var statusValue = reader.GetInt32(5);
        if (!Enum.IsDefined(typeof(OperationExecutionStatus), statusValue))
        {
            throw new InvalidDataException(
                $"O estado operacional persistido ({statusValue}) é inválido.");
        }

        var execution = new OperationExecution(
            ParseGuid(reader.GetString(0), "execução"),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4)
                ? null
                : ParseGuid(reader.GetString(4), "projeto"),
            (OperationExecutionStatus)statusValue,
            reader.GetInt32(6),
            reader.GetString(7),
            ParseTimestamp(reader.GetString(8)),
            reader.IsDBNull(9)
                ? null
                : ParseTimestamp(reader.GetString(9)),
            reader.IsDBNull(10)
                ? null
                : ParseTimestamp(reader.GetString(10)),
            reader.GetInt32(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13));
        ValidateExecution(execution);
        return execution;
    }

    private static void ValidateExecution(OperationExecution execution)
    {
        if (execution.Id == Guid.Empty)
        {
            throw new InvalidDataException(
                "O identificador da execução é inválido.");
        }

        if (string.IsNullOrWhiteSpace(execution.OperationType)
            || string.IsNullOrWhiteSpace(execution.DisplayName)
            || string.IsNullOrWhiteSpace(execution.ResourceKey)
            || string.IsNullOrWhiteSpace(execution.CurrentStep))
        {
            throw new InvalidDataException(
                "A execução contém campos operacionais obrigatórios vazios.");
        }

        if (!Enum.IsDefined(execution.Status)
            || execution.Progress is < 0 or > 100
            || execution.TimeoutSeconds <= 0)
        {
            throw new InvalidDataException(
                "A execução contém estado, progresso ou timeout inválido.");
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
                "Uma data de execução persistida é inválida.");
}
