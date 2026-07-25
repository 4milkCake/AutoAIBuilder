using System.Globalization;
using AutoAIBuilder.Application.Automation.Catalog;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class SqliteAutomationMaskCatalogRepository :
    IAutomationMaskCatalogRepository
{
    private const string SelectColumns =
        """
        SELECT
            Id,
            MaskId,
            MaskVersion,
            MaskName,
            Discipline,
            Description,
            MinimumApplicationVersion,
            RuleCatalogId,
            RuleCatalogVersion,
            RuleCount,
            DependencyCount,
            ParameterCount,
            OutputCount,
            SupportsSimulation,
            IsIdempotent,
            MaskJson,
            RuleCatalogJson,
            ContentSha256,
            MaskSourceFileName,
            RuleCatalogSourceFileName,
            IsActive,
            ImportedAt,
            UpdatedAt
        FROM AutomationMaskCatalog
        """;

    private readonly SqliteDatabase _database;

    public SqliteAutomationMaskCatalogRepository(SqliteDatabase database)
    {
        _database = database
            ?? throw new ArgumentNullException(nameof(database));
        _database.Initialize();
    }

    public IReadOnlyList<AutomationMaskCatalogEntry> GetAll()
    {
        lock (_database.SyncRoot)
        {
            using var connection = _database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                SelectColumns
                + """
                   ORDER BY IsActive DESC, MaskName COLLATE NOCASE,
                            MaskVersion DESC;
                  """;
            using var reader = command.ExecuteReader();
            var entries = new List<AutomationMaskCatalogEntry>();
            while (reader.Read())
            {
                entries.Add(ReadEntry(reader));
            }

            return entries;
        }
    }

    public AutomationMaskCatalogEntry? Get(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        lock (_database.SyncRoot)
        {
            using var connection = _database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                SelectColumns + " WHERE Id = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadEntry(reader) : null;
        }
    }

    public AutomationMaskCatalogEntry? GetByIdentity(
        string maskId,
        string maskVersion)
    {
        if (string.IsNullOrWhiteSpace(maskId)
            || string.IsNullOrWhiteSpace(maskVersion))
        {
            return null;
        }

        lock (_database.SyncRoot)
        {
            using var connection = _database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                SelectColumns
                + """
                   WHERE MaskId = $maskId AND MaskVersion = $maskVersion
                   LIMIT 1;
                  """;
            command.Parameters.AddWithValue("$maskId", maskId);
            command.Parameters.AddWithValue("$maskVersion", maskVersion);
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadEntry(reader) : null;
        }
    }

    public void Add(AutomationMaskCatalogEntry entry)
    {
        Validate(entry);
        lock (_database.SyncRoot)
        {
            using var connection = _database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var existing = connection.CreateCommand();
            existing.Transaction = transaction;
            existing.CommandText =
                """
                SELECT 1
                FROM AutomationMaskCatalog
                WHERE MaskId = $maskId AND MaskVersion = $maskVersion
                LIMIT 1;
                """;
            existing.Parameters.AddWithValue("$maskId", entry.MaskId);
            existing.Parameters.AddWithValue(
                "$maskVersion",
                entry.MaskVersion);
            if (existing.ExecuteScalar() is not null)
            {
                throw new InvalidOperationException(
                    $"A máscara '{entry.MaskId}@{entry.MaskVersion}' já existe.");
            }

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO AutomationMaskCatalog (
                    Id,
                    MaskId,
                    MaskVersion,
                    MaskName,
                    Discipline,
                    Description,
                    MinimumApplicationVersion,
                    RuleCatalogId,
                    RuleCatalogVersion,
                    RuleCount,
                    DependencyCount,
                    ParameterCount,
                    OutputCount,
                    SupportsSimulation,
                    IsIdempotent,
                    MaskJson,
                    RuleCatalogJson,
                    ContentSha256,
                    MaskSourceFileName,
                    RuleCatalogSourceFileName,
                    IsActive,
                    ImportedAt,
                    UpdatedAt)
                VALUES (
                    $id,
                    $maskId,
                    $maskVersion,
                    $maskName,
                    $discipline,
                    $description,
                    $minimumApplicationVersion,
                    $ruleCatalogId,
                    $ruleCatalogVersion,
                    $ruleCount,
                    $dependencyCount,
                    $parameterCount,
                    $outputCount,
                    $supportsSimulation,
                    $isIdempotent,
                    $maskJson,
                    $ruleCatalogJson,
                    $contentSha256,
                    $maskSourceFileName,
                    $ruleCatalogSourceFileName,
                    $isActive,
                    $importedAt,
                    $updatedAt);
                """;
            AddParameters(insert, entry);
            insert.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    public AutomationMaskCatalogEntry SetActive(
        Guid id,
        bool isActive,
        DateTimeOffset updatedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador da máscara é obrigatório.",
                nameof(id));
        }

        lock (_database.SyncRoot)
        {
            using var connection = _database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var find = connection.CreateCommand();
            find.Transaction = transaction;
            find.CommandText =
                """
                SELECT MaskId
                FROM AutomationMaskCatalog
                WHERE Id = $id
                LIMIT 1;
                """;
            find.Parameters.AddWithValue("$id", id.ToString("D"));
            var maskId = find.ExecuteScalar() as string
                ?? throw new InvalidOperationException(
                    "A máscara selecionada não existe mais no catálogo.");
            var timestamp = updatedAt.UtcDateTime.ToString("O");

            if (isActive)
            {
                using var deactivate = connection.CreateCommand();
                deactivate.Transaction = transaction;
                deactivate.CommandText =
                    """
                    UPDATE AutomationMaskCatalog
                    SET IsActive = 0, UpdatedAt = $updatedAt
                    WHERE MaskId = $maskId AND Id <> $id AND IsActive = 1;
                    """;
                deactivate.Parameters.AddWithValue("$updatedAt", timestamp);
                deactivate.Parameters.AddWithValue("$maskId", maskId);
                deactivate.Parameters.AddWithValue("$id", id.ToString("D"));
                deactivate.ExecuteNonQuery();
            }

            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE AutomationMaskCatalog
                SET IsActive = $isActive, UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;
            update.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
            update.Parameters.AddWithValue("$updatedAt", timestamp);
            update.Parameters.AddWithValue("$id", id.ToString("D"));
            update.ExecuteNonQuery();
            transaction.Commit();
        }

        return Get(id)
            ?? throw new InvalidOperationException(
                "Não foi possível reler a máscara atualizada.");
    }

    private static void AddParameters(
        Microsoft.Data.Sqlite.SqliteCommand command,
        AutomationMaskCatalogEntry entry)
    {
        command.Parameters.AddWithValue("$id", entry.Id.ToString("D"));
        command.Parameters.AddWithValue("$maskId", entry.MaskId);
        command.Parameters.AddWithValue("$maskVersion", entry.MaskVersion);
        command.Parameters.AddWithValue("$maskName", entry.MaskName);
        command.Parameters.AddWithValue("$discipline", entry.Discipline);
        command.Parameters.AddWithValue("$description", entry.Description);
        command.Parameters.AddWithValue(
            "$minimumApplicationVersion",
            entry.MinimumApplicationVersion);
        command.Parameters.AddWithValue(
            "$ruleCatalogId",
            entry.RuleCatalogId);
        command.Parameters.AddWithValue(
            "$ruleCatalogVersion",
            entry.RuleCatalogVersion);
        command.Parameters.AddWithValue("$ruleCount", entry.RuleCount);
        command.Parameters.AddWithValue(
            "$dependencyCount",
            entry.DependencyCount);
        command.Parameters.AddWithValue(
            "$parameterCount",
            entry.ParameterCount);
        command.Parameters.AddWithValue("$outputCount", entry.OutputCount);
        command.Parameters.AddWithValue(
            "$supportsSimulation",
            entry.SupportsSimulation ? 1 : 0);
        command.Parameters.AddWithValue(
            "$isIdempotent",
            entry.IsIdempotent ? 1 : 0);
        command.Parameters.AddWithValue("$maskJson", entry.MaskJson);
        command.Parameters.AddWithValue(
            "$ruleCatalogJson",
            entry.RuleCatalogJson);
        command.Parameters.AddWithValue(
            "$contentSha256",
            entry.ContentSha256);
        command.Parameters.AddWithValue(
            "$maskSourceFileName",
            entry.MaskSourceFileName);
        command.Parameters.AddWithValue(
            "$ruleCatalogSourceFileName",
            entry.RuleCatalogSourceFileName);
        command.Parameters.AddWithValue(
            "$isActive",
            entry.IsActive ? 1 : 0);
        command.Parameters.AddWithValue(
            "$importedAt",
            entry.ImportedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue(
            "$updatedAt",
            entry.UpdatedAt.UtcDateTime.ToString("O"));
    }

    private static AutomationMaskCatalogEntry ReadEntry(
        Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        try
        {
            var entry = new AutomationMaskCatalogEntry(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                ReadBoolean(reader, 13),
                ReadBoolean(reader, 14),
                reader.GetString(15),
                reader.GetString(16),
                reader.GetString(17),
                reader.GetString(18),
                reader.GetString(19),
                ReadBoolean(reader, 20),
                ParseTimestamp(reader.GetString(21)),
                ParseTimestamp(reader.GetString(22)));
            Validate(entry);
            return entry;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or FormatException
                or OverflowException
                or InvalidOperationException)
        {
            throw new InvalidDataException(
                "O catálogo de máscaras contém um registro inválido. "
                + "O conteúdo foi preservado e não será utilizado.",
                exception);
        }
    }

    private static bool ReadBoolean(
        Microsoft.Data.Sqlite.SqliteDataReader reader,
        int ordinal)
    {
        var value = reader.GetInt32(ordinal);
        return value switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidDataException(
                "O catálogo contém um valor lógico inválido.")
        };
    }

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

    private static void Validate(AutomationMaskCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(entry.MaskId)
            || string.IsNullOrWhiteSpace(entry.MaskVersion)
            || string.IsNullOrWhiteSpace(entry.MaskName)
            || string.IsNullOrWhiteSpace(entry.Discipline)
            || string.IsNullOrWhiteSpace(entry.Description)
            || string.IsNullOrWhiteSpace(entry.MinimumApplicationVersion)
            || string.IsNullOrWhiteSpace(entry.RuleCatalogId)
            || string.IsNullOrWhiteSpace(entry.RuleCatalogVersion)
            || entry.RuleCount < 0
            || entry.DependencyCount < 0
            || entry.ParameterCount < 0
            || entry.OutputCount < 0
            || string.IsNullOrWhiteSpace(entry.MaskJson)
            || string.IsNullOrWhiteSpace(entry.RuleCatalogJson)
            || !IsSha256(entry.ContentSha256)
            || !IsSafeSourceFileName(entry.MaskSourceFileName)
            || !IsSafeSourceFileName(entry.RuleCatalogSourceFileName)
            || entry.ImportedAt == default
            || entry.UpdatedAt == default)
        {
            throw new InvalidDataException(
                "O registro da máscara não atende ao contrato de persistência.");
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

    private static bool IsSafeSourceFileName(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal)
        && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
