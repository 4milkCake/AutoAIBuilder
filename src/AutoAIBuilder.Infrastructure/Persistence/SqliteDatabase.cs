using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class SqliteDatabase
{
    public const int CurrentSchemaVersion = 5;
    private const int CommandTimeoutSeconds = 30;

    private readonly string _connectionString;
    private readonly object _sync = new();
    private bool _initialized;

    public SqliteDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException(
                "O caminho do banco de dados é obrigatório.",
                nameof(databasePath));
        }

        DatabasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = true
        }.ToString();
    }

    public string DatabasePath { get; }

    internal object SyncRoot => _sync;

    public void Initialize()
    {
        lock (_sync)
        {
            if (_initialized)
            {
                return;
            }

            var directory = Path.GetDirectoryName(DatabasePath)
                ?? throw new InvalidOperationException(
                    "Não foi possível determinar a pasta do banco de dados.");
            Directory.CreateDirectory(directory);

            using var connection = OpenConnectionCore();
            ExecuteNonQuery(
                connection,
                """
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA foreign_keys = ON;
                PRAGMA busy_timeout = 30000;
                """);

            var version = ReadSchemaVersion(connection);
            if (version > CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"O banco usa o esquema {version}, superior ao suportado "
                    + $"por esta versão ({CurrentSchemaVersion}).");
            }

            if (version == 0)
            {
                ApplyVersion1(connection);
                version = ReadSchemaVersion(connection);
            }

            if (version == 1)
            {
                ApplyVersion2(connection);
                version = ReadSchemaVersion(connection);
            }

            if (version == 2)
            {
                ApplyVersion3(connection);
                version = ReadSchemaVersion(connection);
            }

            if (version == 3)
            {
                ApplyVersion4(connection);
                version = ReadSchemaVersion(connection);
            }

            if (version == 4)
            {
                ApplyVersion5(connection);
                version = ReadSchemaVersion(connection);
            }

            if (version != CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Não foi possível atualizar o banco para o esquema "
                    + $"{CurrentSchemaVersion}.");
            }

            _initialized = true;
        }
    }

    public SqliteConnection OpenConnection()
    {
        Initialize();
        return OpenConnectionCore();
    }

    public int GetSchemaVersion()
    {
        using var connection = OpenConnection();
        return ReadSchemaVersion(connection);
    }

    public string QuickCheck()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        command.CommandTimeout = CommandTimeoutSeconds;
        return Convert.ToString(command.ExecuteScalar()) ?? "resultado indisponível";
    }

    public bool HasDataMigration(string name)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT 1 FROM DataMigrations WHERE Name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", name);
        command.CommandTimeout = CommandTimeoutSeconds;
        return command.ExecuteScalar() is not null;
    }

    public void MarkDataMigration(string name, string detail)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO DataMigrations (Name, AppliedAt, Detail)
            VALUES ($name, $appliedAt, $detail)
            ON CONFLICT(Name) DO UPDATE SET
                AppliedAt = excluded.AppliedAt,
                Detail = excluded.Detail;
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue(
            "$appliedAt",
            DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$detail", detail);
        command.CommandTimeout = CommandTimeoutSeconds;
        command.ExecuteNonQuery();
    }

    public void ResetInitialization()
    {
        lock (_sync)
        {
            _initialized = false;
        }
    }

    private SqliteConnection OpenConnectionCore()
    {
        var connection = new SqliteConnection(_connectionString)
        {
            DefaultTimeout = CommandTimeoutSeconds
        };
        connection.Open();

        ExecuteNonQuery(
            connection,
            """
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 30000;
            """);
        return connection;
    }

    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        command.CommandTimeout = CommandTimeoutSeconds;
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void ApplyVersion1(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            CREATE TABLE Projects (
                Id TEXT NOT NULL PRIMARY KEY,
                PayloadJson TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX IX_Projects_UpdatedAt
                ON Projects (UpdatedAt DESC);

            CREATE TABLE ApplicationSettings (
                Id INTEGER NOT NULL PRIMARY KEY CHECK (Id = 1),
                PayloadJson TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE ActivityLog (
                Id TEXT NOT NULL PRIMARY KEY,
                OccurredAt TEXT NOT NULL,
                Category TEXT NOT NULL,
                Action TEXT NOT NULL,
                Description TEXT NOT NULL,
                Level INTEGER NOT NULL,
                ProjectId TEXT NULL,
                ProjectName TEXT NULL
            );

            CREATE INDEX IX_ActivityLog_OccurredAt
                ON ActivityLog (OccurredAt DESC);

            CREATE TABLE AppState (
                Key TEXT NOT NULL PRIMARY KEY,
                Value TEXT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE DataMigrations (
                Name TEXT NOT NULL PRIMARY KEY,
                AppliedAt TEXT NOT NULL,
                Detail TEXT NOT NULL
            );

            CREATE TABLE SchemaMigrations (
                Version INTEGER NOT NULL PRIMARY KEY,
                AppliedAt TEXT NOT NULL,
                Description TEXT NOT NULL
            );

            INSERT INTO SchemaMigrations (Version, AppliedAt, Description)
            VALUES (1, $appliedAt, 'Esquema SQLite inicial e migração dos JSON legados');

            PRAGMA user_version = 1;
            """;
        command.Parameters.AddWithValue(
            "$appliedAt",
            DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static void ApplyVersion2(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            CREATE TABLE OperationExecutions (
                Id TEXT NOT NULL PRIMARY KEY,
                OperationType TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                ResourceKey TEXT NOT NULL,
                ProjectId TEXT NULL,
                Status INTEGER NOT NULL,
                Progress INTEGER NOT NULL,
                CurrentStep TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                StartedAt TEXT NULL,
                CompletedAt TEXT NULL,
                TimeoutSeconds INTEGER NOT NULL,
                ErrorCode TEXT NULL,
                ErrorMessage TEXT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX IX_OperationExecutions_CreatedAt
                ON OperationExecutions (CreatedAt DESC);

            CREATE INDEX IX_OperationExecutions_Status
                ON OperationExecutions (Status, UpdatedAt DESC);

            INSERT INTO SchemaMigrations (Version, AppliedAt, Description)
            VALUES (
                2,
                $appliedAt,
                'Estado persistente do motor de operações assíncronas');

            PRAGMA user_version = 2;
            """;
        command.Parameters.AddWithValue(
            "$appliedAt",
            DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static void ApplyVersion3(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            CREATE TABLE AutomationAudits (
                Id TEXT NOT NULL PRIMARY KEY,
                PlanId TEXT NOT NULL,
                ProjectId TEXT NOT NULL,
                MaskId TEXT NOT NULL,
                MaskVersion TEXT NOT NULL,
                Mode INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                IdempotencyKey TEXT NOT NULL,
                InputsJson TEXT NOT NULL,
                OutputsJson TEXT NOT NULL,
                PublishedPath TEXT NULL,
                RecoveryPath TEXT NULL,
                Summary TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                CompletedAt TEXT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX IX_AutomationAudits_CreatedAt
                ON AutomationAudits (CreatedAt DESC);

            CREATE INDEX IX_AutomationAudits_Idempotency
                ON AutomationAudits (IdempotencyKey, Status, CompletedAt DESC);

            INSERT INTO SchemaMigrations (Version, AppliedAt, Description)
            VALUES (
                3,
                $appliedAt,
                'Contratos seguros, simulações e auditoria das automações');

            PRAGMA user_version = 3;
            """;
        command.Parameters.AddWithValue(
            "$appliedAt",
            DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static void ApplyVersion4(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            CREATE TABLE AutomationMaskCatalog (
                Id TEXT NOT NULL PRIMARY KEY,
                MaskId TEXT NOT NULL,
                MaskVersion TEXT NOT NULL,
                MaskName TEXT NOT NULL,
                Discipline TEXT NOT NULL,
                Description TEXT NOT NULL,
                MinimumApplicationVersion TEXT NOT NULL,
                RuleCatalogId TEXT NOT NULL,
                RuleCatalogVersion TEXT NOT NULL,
                RuleCount INTEGER NOT NULL CHECK (RuleCount >= 0),
                DependencyCount INTEGER NOT NULL CHECK (DependencyCount >= 0),
                ParameterCount INTEGER NOT NULL CHECK (ParameterCount >= 0),
                OutputCount INTEGER NOT NULL CHECK (OutputCount >= 0),
                SupportsSimulation INTEGER NOT NULL
                    CHECK (SupportsSimulation IN (0, 1)),
                IsIdempotent INTEGER NOT NULL
                    CHECK (IsIdempotent IN (0, 1)),
                MaskJson TEXT NOT NULL,
                RuleCatalogJson TEXT NOT NULL,
                ContentSha256 TEXT NOT NULL,
                MaskSourceFileName TEXT NOT NULL,
                RuleCatalogSourceFileName TEXT NOT NULL,
                IsActive INTEGER NOT NULL CHECK (IsActive IN (0, 1)),
                ImportedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                UNIQUE (MaskId, MaskVersion)
            );

            CREATE INDEX IX_AutomationMaskCatalog_Name
                ON AutomationMaskCatalog (MaskName, MaskVersion);

            CREATE INDEX IX_AutomationMaskCatalog_Active
                ON AutomationMaskCatalog (IsActive DESC, UpdatedAt DESC);

            CREATE UNIQUE INDEX IX_AutomationMaskCatalog_OneActiveVersion
                ON AutomationMaskCatalog (MaskId)
                WHERE IsActive = 1;

            INSERT INTO SchemaMigrations (Version, AppliedAt, Description)
            VALUES (
                4,
                $appliedAt,
                'Catálogo seguro e versionado de máscaras de automação');

            PRAGMA user_version = 4;
            """;
        command.Parameters.AddWithValue(
            "$appliedAt",
            DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static void ApplyVersion5(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            ALTER TABLE AutomationAudits
                ADD COLUMN RuleCatalogId TEXT NULL;

            ALTER TABLE AutomationAudits
                ADD COLUMN RuleCatalogVersion TEXT NULL;

            ALTER TABLE AutomationAudits
                ADD COLUMN ContractSha256 TEXT NULL;

            ALTER TABLE AutomationAudits
                ADD COLUMN AdapterId TEXT NULL;

            ALTER TABLE AutomationAudits
                ADD COLUMN AdapterVersion TEXT NULL;

            CREATE TABLE AutomationIntegrationAssessments (
                Id TEXT NOT NULL PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                CatalogEntryId TEXT NOT NULL,
                MaskId TEXT NOT NULL,
                MaskVersion TEXT NOT NULL,
                ContractSha256 TEXT NOT NULL,
                Status INTEGER NOT NULL,
                AdapterId TEXT NULL,
                AdapterVersion TEXT NULL,
                Summary TEXT NOT NULL,
                EvaluatedAt TEXT NOT NULL,
                FOREIGN KEY (CatalogEntryId)
                    REFERENCES AutomationMaskCatalog (Id)
            );

            CREATE INDEX IX_AutomationIntegrationAssessments_EvaluatedAt
                ON AutomationIntegrationAssessments (EvaluatedAt DESC);

            CREATE INDEX IX_AutomationIntegrationAssessments_Mask
                ON AutomationIntegrationAssessments (
                    MaskId,
                    MaskVersion,
                    EvaluatedAt DESC);

            INSERT INTO SchemaMigrations (Version, AppliedAt, Description)
            VALUES (
                5,
                $appliedAt,
                'Registro interno de adaptadores e auditoria da prontidão de integração');

            PRAGMA user_version = 5;
            """;
        command.Parameters.AddWithValue(
            "$appliedAt",
            DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static void ExecuteNonQuery(
        SqliteConnection connection,
        string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.ExecuteNonQuery();
    }
}
