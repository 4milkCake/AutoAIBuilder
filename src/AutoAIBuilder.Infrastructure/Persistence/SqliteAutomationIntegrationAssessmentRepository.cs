using System.Globalization;
using AutoAIBuilder.Application.Automation.Orchestration;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class SqliteAutomationIntegrationAssessmentRepository(
    SqliteDatabase database) :
    IAutomationIntegrationAssessmentRepository
{
    private const string SelectColumns =
        """
        SELECT
            Id,
            ProjectId,
            CatalogEntryId,
            MaskId,
            MaskVersion,
            ContractSha256,
            Status,
            AdapterId,
            AdapterVersion,
            Summary,
            EvaluatedAt
        FROM AutomationIntegrationAssessments
        """;

    public IReadOnlyList<AutomationIntegrationAssessment> GetRecent(
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
                + " ORDER BY EvaluatedAt DESC LIMIT $maximumEntries;";
            command.Parameters.AddWithValue(
                "$maximumEntries",
                maximumEntries);
            using var reader = command.ExecuteReader();
            var entries = new List<AutomationIntegrationAssessment>();
            while (reader.Read())
            {
                entries.Add(Read(reader));
            }

            return entries;
        }
    }

    public void Add(AutomationIntegrationAssessment assessment)
    {
        Validate(assessment);
        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO AutomationIntegrationAssessments (
                    Id,
                    ProjectId,
                    CatalogEntryId,
                    MaskId,
                    MaskVersion,
                    ContractSha256,
                    Status,
                    AdapterId,
                    AdapterVersion,
                    Summary,
                    EvaluatedAt)
                VALUES (
                    $id,
                    $projectId,
                    $catalogEntryId,
                    $maskId,
                    $maskVersion,
                    $contractSha256,
                    $status,
                    $adapterId,
                    $adapterVersion,
                    $summary,
                    $evaluatedAt);
                """;
            command.Parameters.AddWithValue(
                "$id",
                assessment.Id.ToString("D"));
            command.Parameters.AddWithValue(
                "$projectId",
                assessment.ProjectId.ToString("D"));
            command.Parameters.AddWithValue(
                "$catalogEntryId",
                assessment.CatalogEntryId.ToString("D"));
            command.Parameters.AddWithValue("$maskId", assessment.MaskId);
            command.Parameters.AddWithValue(
                "$maskVersion",
                assessment.MaskVersion);
            command.Parameters.AddWithValue(
                "$contractSha256",
                assessment.ContractSha256);
            command.Parameters.AddWithValue(
                "$status",
                (int)assessment.Status);
            command.Parameters.AddWithValue(
                "$adapterId",
                assessment.AdapterId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue(
                "$adapterVersion",
                assessment.AdapterVersion ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$summary", assessment.Summary);
            command.Parameters.AddWithValue(
                "$evaluatedAt",
                assessment.EvaluatedAt.UtcDateTime.ToString("O"));
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    private static AutomationIntegrationAssessment Read(
        SqliteDataReader reader)
    {
        try
        {
            var statusValue = reader.GetInt32(6);
            if (!Enum.IsDefined(
                    typeof(AutomationIntegrationStatus),
                    statusValue))
            {
                throw new InvalidDataException(
                    "A avaliação contém um estado desconhecido.");
            }

            var entry = new AutomationIntegrationAssessment(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                (AutomationIntegrationStatus)statusValue,
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9),
                DateTimeOffset.Parse(
                    reader.GetString(10),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind));
            Validate(entry);
            return entry;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or FormatException
                or InvalidOperationException
                or OverflowException)
        {
            throw new InvalidDataException(
                "Uma avaliação de integração persistida é inválida. O registro "
                + "foi preservado e não será utilizado.",
                exception);
        }
    }

    private static void Validate(
        AutomationIntegrationAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        if (assessment.Id == Guid.Empty
            || assessment.ProjectId == Guid.Empty
            || assessment.CatalogEntryId == Guid.Empty
            || string.IsNullOrWhiteSpace(assessment.MaskId)
            || string.IsNullOrWhiteSpace(assessment.MaskVersion)
            || !IsSha256(assessment.ContractSha256)
            || !Enum.IsDefined(assessment.Status)
            || string.IsNullOrWhiteSpace(assessment.Summary)
            || assessment.Summary.Length > 4_096
            || assessment.EvaluatedAt == default
            || ((assessment.AdapterId is null)
                != (assessment.AdapterVersion is null)))
        {
            throw new InvalidDataException(
                "A avaliação de integração não atende ao contrato de "
                + "persistência.");
        }
    }

    private static bool IsSha256(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        try
        {
            return Convert.FromHexString(value).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
