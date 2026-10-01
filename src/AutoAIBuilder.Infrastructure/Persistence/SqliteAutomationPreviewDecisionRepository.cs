using System.Globalization;
using AutoAIBuilder.Application.Automation.Preview;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class SqliteAutomationPreviewDecisionRepository(
    SqliteDatabase database) :
    IAutomationPreviewDecisionRepository
{
    public IReadOnlyList<AutomationPreviewDecision> GetForPlan(string planId)
    {
        ValidatePlanId(planId);
        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    Id,
                    PlanId,
                    ProjectId,
                    DatasetId,
                    GroupId,
                    Decision,
                    Note,
                    DecidedAt
                FROM AutomationPreviewDecisions
                WHERE PlanId = $planId
                ORDER BY DecidedAt DESC;
                """;
            command.Parameters.AddWithValue("$planId", planId);
            using var reader = command.ExecuteReader();
            var result = new List<AutomationPreviewDecision>();
            while (reader.Read())
            {
                result.Add(Read(reader));
            }

            return result;
        }
    }

    public void Save(AutomationPreviewDecision decision)
    {
        Validate(decision);
        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO AutomationPreviewDecisions (
                    Id,
                    PlanId,
                    ProjectId,
                    DatasetId,
                    GroupId,
                    Decision,
                    Note,
                    DecidedAt)
                VALUES (
                    $id,
                    $planId,
                    $projectId,
                    $datasetId,
                    $groupId,
                    $decision,
                    $note,
                    $decidedAt)
                ON CONFLICT(PlanId, GroupId) DO UPDATE SET
                    Id = excluded.Id,
                    Decision = excluded.Decision,
                    Note = excluded.Note,
                    DecidedAt = excluded.DecidedAt;
                """;
            command.Parameters.AddWithValue("$id", decision.Id.ToString("D"));
            command.Parameters.AddWithValue("$planId", decision.PlanId);
            command.Parameters.AddWithValue(
                "$projectId",
                decision.ProjectId.ToString("D"));
            command.Parameters.AddWithValue(
                "$datasetId",
                decision.DatasetId.ToString("D"));
            command.Parameters.AddWithValue("$groupId", decision.GroupId);
            command.Parameters.AddWithValue("$decision", (int)decision.Status);
            command.Parameters.AddWithValue(
                "$note",
                decision.Note ?? (object)DBNull.Value);
            command.Parameters.AddWithValue(
                "$decidedAt",
                decision.DecidedAt.UtcDateTime.ToString("O"));
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    private static AutomationPreviewDecision Read(SqliteDataReader reader)
    {
        var value = reader.GetInt32(5);
        if (!Enum.IsDefined(typeof(AutomationPreviewDecisionStatus), value))
        {
            throw new InvalidDataException(
                "A decisão de pré-visualização contém um estado desconhecido.");
        }

        var decision = new AutomationPreviewDecision(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            Guid.Parse(reader.GetString(2)),
            Guid.Parse(reader.GetString(3)),
            reader.GetString(4),
            (AutomationPreviewDecisionStatus)value,
            reader.IsDBNull(6) ? null : reader.GetString(6),
            DateTimeOffset.Parse(
                reader.GetString(7),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind));
        Validate(decision);
        return decision;
    }

    private static void Validate(AutomationPreviewDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ValidatePlanId(decision.PlanId);
        if (decision.Id == Guid.Empty
            || decision.ProjectId == Guid.Empty
            || decision.DatasetId == Guid.Empty
            || string.IsNullOrWhiteSpace(decision.GroupId)
            || decision.GroupId.Length > 120
            || decision.Status is not (
                AutomationPreviewDecisionStatus.Approved
                or AutomationPreviewDecisionStatus.Rejected)
            || decision.Note?.Length > 1_000
            || decision.DecidedAt == default)
        {
            throw new InvalidDataException(
                "A decisão de pré-visualização não atende ao contrato.");
        }
    }

    private static void ValidatePlanId(string planId)
    {
        if (string.IsNullOrWhiteSpace(planId) || planId.Length != 64)
        {
            throw new ArgumentException(
                "O identificador do plano deve ser um SHA-256.",
                nameof(planId));
        }

        try
        {
            if (Convert.FromHexString(planId).Length != 32)
            {
                throw new FormatException();
            }
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "O identificador do plano deve ser um SHA-256.",
                nameof(planId),
                exception);
        }
    }
}
