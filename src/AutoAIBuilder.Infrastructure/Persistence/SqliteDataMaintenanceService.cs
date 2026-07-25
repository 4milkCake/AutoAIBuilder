using AutoAIBuilder.Application.Maintenance;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class SqliteDataMaintenanceService : IDataMaintenanceService
{
    private readonly SqliteDatabase _database;
    private readonly Action<string> _saveDataDirectory;

    public SqliteDataMaintenanceService(
        SqliteDatabase database,
        Action<string>? saveDataDirectory = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _saveDataDirectory =
            saveDataDirectory
            ?? StorageLocationConfiguration.ScheduleDataDirectoryChange;
        _database.Initialize();
    }

    public string DataDirectory =>
        Path.GetDirectoryName(_database.DatabasePath)
        ?? throw new InvalidOperationException(
            "Não foi possível determinar a pasta do banco de dados.");

    public string DatabasePath => _database.DatabasePath;

    public string BackupDirectory => Path.Combine(DataDirectory, "Backups");

    public int SchemaVersion => _database.GetSchemaVersion();

    public DataBackupResult CreateBackup(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException(
                "O caminho do backup é obrigatório.",
                nameof(destinationPath));
        }

        var normalizedDestination = Path.GetFullPath(destinationPath);
        if (PathsEqual(normalizedDestination, DatabasePath))
        {
            throw new InvalidOperationException(
                "O backup não pode substituir diretamente o banco de dados em uso.");
        }

        var directory = Path.GetDirectoryName(normalizedDestination)
            ?? throw new InvalidOperationException(
                "Não foi possível determinar a pasta do backup.");
        Directory.CreateDirectory(directory);

        var temporaryPath =
            $"{normalizedDestination}.{Guid.NewGuid():N}.tmp";

        lock (_database.SyncRoot)
        {
            try
            {
                using var source = _database.OpenConnection();
                using var destination = OpenBackupConnection(
                    temporaryPath,
                    SqliteOpenMode.ReadWriteCreate);
                source.BackupDatabase(destination);
                destination.Close();

                ValidateDatabaseFile(temporaryPath);
                File.Move(
                    temporaryPath,
                    normalizedDestination,
                    overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        var info = new FileInfo(normalizedDestination);
        return new DataBackupResult(
            normalizedDestination,
            DateTimeOffset.Now,
            info.Length);
    }

    public DataRestoreResult RestoreBackup(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException(
                "O arquivo de backup é obrigatório.",
                nameof(sourcePath));
        }

        var normalizedSource = Path.GetFullPath(sourcePath);
        if (!File.Exists(normalizedSource))
        {
            throw new FileNotFoundException(
                "O arquivo de backup selecionado não existe.",
                normalizedSource);
        }

        if (PathsEqual(normalizedSource, DatabasePath))
        {
            throw new InvalidOperationException(
                "Selecione um backup diferente do banco de dados em uso.");
        }

        ValidateDatabaseFile(normalizedSource);

        Directory.CreateDirectory(BackupDirectory);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var safetyBackupPath = Path.Combine(
            BackupDirectory,
            $"antes-restauracao-{timestamp}.aabbackup");
        CreateBackup(safetyBackupPath);

        lock (_database.SyncRoot)
        {
            SqliteConnection.ClearAllPools();

            try
            {
                RestoreDatabaseFile(normalizedSource);
                _database.ResetInitialization();
                _database.Initialize();

                if (!string.Equals(
                        _database.QuickCheck(),
                        "ok",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "O banco restaurado não passou na verificação de integridade.");
                }
            }
            catch (Exception restoreException)
            {
                try
                {
                    RestoreDatabaseFile(safetyBackupPath);
                    _database.ResetInitialization();
                    _database.Initialize();
                }
                catch (Exception rollbackException)
                {
                    throw new InvalidOperationException(
                        "A restauração falhou e o retorno automático ao estado "
                        + "anterior também não foi concluído.",
                        new AggregateException(restoreException, rollbackException));
                }

                throw new InvalidOperationException(
                    "A restauração não foi concluída. O banco anterior foi "
                    + "recuperado automaticamente.",
                    restoreException);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
            }
        }

        return new DataRestoreResult(
            normalizedSource,
            safetyBackupPath,
            DateTimeOffset.Now);
    }

    public DataRelocationResult RelocateDataDirectory(
        string destinationDirectory)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new ArgumentException(
                "A nova pasta de dados é obrigatória.",
                nameof(destinationDirectory));
        }

        var normalizedDirectory = Path.GetFullPath(destinationDirectory);
        if (PathsEqual(normalizedDirectory, DataDirectory))
        {
            return new DataRelocationResult(
                DataDirectory,
                normalizedDirectory,
                DatabasePath,
                RequiresRestart: false);
        }

        Directory.CreateDirectory(normalizedDirectory);
        var newDatabasePath = Path.Combine(
            normalizedDirectory,
            Path.GetFileName(DatabasePath));

        if (File.Exists(newDatabasePath))
        {
            throw new InvalidOperationException(
                "A pasta escolhida já contém um banco do AutoAIBuilder. "
                + "Selecione uma pasta vazia para evitar sobrescrita.");
        }

        CreateBackup(newDatabasePath);
        _saveDataDirectory(normalizedDirectory);

        return new DataRelocationResult(
            DataDirectory,
            normalizedDirectory,
            newDatabasePath,
            RequiresRestart: true);
    }

    private void RestoreDatabaseFile(string sourcePath)
    {
        using var source = OpenBackupConnection(
            sourcePath,
            SqliteOpenMode.ReadOnly);
        using var destination = _database.OpenConnection();
        source.BackupDatabase(destination);
    }

    private static void ValidateDatabaseFile(string filePath)
    {
        using var connection = OpenBackupConnection(
            filePath,
            SqliteOpenMode.ReadOnly);

        using (var check = connection.CreateCommand())
        {
            check.CommandText = "PRAGMA quick_check;";
            var result = Convert.ToString(check.ExecuteScalar());
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"O arquivo não passou na verificação SQLite: {result}.");
            }
        }

        int version;
        using (var versionCommand = connection.CreateCommand())
        {
            versionCommand.CommandText = "PRAGMA user_version;";
            version = Convert.ToInt32(versionCommand.ExecuteScalar());
            if (version < 1 || version > SqliteDatabase.CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"O backup usa o esquema {version}, incompatível com a "
                    + $"versão atual ({SqliteDatabase.CurrentSchemaVersion}).");
            }
        }

        using var tables = connection.CreateCommand();
        tables.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN (
                  'Projects',
                  'ApplicationSettings',
                   'ActivityLog',
                   'AppState',
                   'DataMigrations',
                   'SchemaMigrations',
                   'OperationExecutions');
            """;
        var expectedTableCount = version >= 2 ? 7 : 6;
        if (Convert.ToInt32(tables.ExecuteScalar()) != expectedTableCount)
        {
            throw new InvalidDataException(
                "O arquivo selecionado não contém o esquema completo do AutoAIBuilder.");
        }
    }

    private static SqliteConnection OpenBackupConnection(
        string filePath,
        SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = Path.GetFullPath(filePath),
                Mode = mode,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            }.ToString())
        {
            DefaultTimeout = 30
        };
        connection.Open();
        return connection;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
