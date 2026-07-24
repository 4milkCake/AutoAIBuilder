using System.Runtime.InteropServices;
using System.Text.Json;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Infrastructure.Persistence;

namespace AutoAIBuilder.Infrastructure.Diagnostics;

public sealed class EnvironmentDiagnosticService(
    IDiagnosticLogger logger,
    SqliteDatabase database) : IDiagnosticService
{
    public DiagnosticSnapshot Capture()
    {
        var checks = new List<DiagnosticCheck>
        {
            new(
                "Sistema operacional",
                RuntimeInformation.OSDescription,
                OperatingSystem.IsWindows()
                    ? DiagnosticStatus.Healthy
                    : DiagnosticStatus.Error,
                OperatingSystem.IsWindows()
                    ? "Ambiente Windows compatível com a aplicação WPF."
                    : "A aplicação desktop requer Windows."),
            new(
                "Runtime .NET",
                RuntimeInformation.FrameworkDescription,
                Environment.Version.Major == 8
                    ? DiagnosticStatus.Healthy
                    : DiagnosticStatus.Warning,
                $"Processo {RuntimeInformation.ProcessArchitecture}; SO {RuntimeInformation.OSArchitecture}."),
            CreatePathCheck("Dados locais", AppStoragePaths.DataDirectory),
            CreateSqliteCheck(database),
            CreatePathCheck("Backups de segurança", AppStoragePaths.BackupDirectory),
            CreatePathCheck("Log estruturado", logger.StoragePath),
            CreateLegacyJsonCheck("Projetos legados", AppStoragePaths.ProjectsFile),
            CreateLegacyJsonCheck("Configurações legadas", AppStoragePaths.SettingsFile),
            CreateLegacyJsonCheck("Histórico legado", AppStoragePaths.ActivityLogFile)
        };

        return new DiagnosticSnapshot(
            DateTimeOffset.Now,
            checks,
            logger.GetRecent(100));
    }

    private static DiagnosticCheck CreatePathCheck(string name, string path)
    {
        var directory = Path.HasExtension(path)
            ? Path.GetDirectoryName(path)
            : path;
        var exists = directory is not null && Directory.Exists(directory);

        return new DiagnosticCheck(
            name,
            path,
            exists ? DiagnosticStatus.Healthy : DiagnosticStatus.Information,
            exists
                ? "Pasta acessível."
                : "A pasta será criada pelo aplicativo quando houver conteúdo para gravar.");
    }

    private static DiagnosticCheck CreateSqliteCheck(SqliteDatabase database)
    {
        try
        {
            var result = database.QuickCheck();
            var version = database.GetSchemaVersion();
            var info = new FileInfo(database.DatabasePath);
            var healthy = string.Equals(
                result,
                "ok",
                StringComparison.OrdinalIgnoreCase);

            return new DiagnosticCheck(
                "Banco de dados SQLite",
                database.DatabasePath,
                healthy ? DiagnosticStatus.Healthy : DiagnosticStatus.Error,
                healthy
                    ? $"Esquema {version}; {info.Length:N0} bytes; integridade OK."
                    : $"A verificação SQLite retornou: {result}.");
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or InvalidOperationException)
        {
            return new DiagnosticCheck(
                "Banco de dados SQLite",
                database.DatabasePath,
                DiagnosticStatus.Error,
                $"Não foi possível verificar o banco: {exception.Message}");
        }
    }

    private static DiagnosticCheck CreateLegacyJsonCheck(string name, string path)
    {
        if (!File.Exists(path))
        {
            return new DiagnosticCheck(
                name,
                path,
                DiagnosticStatus.Information,
                "Arquivo legado não encontrado; nenhuma migração pendente.");
        }

        try
        {
            var info = new FileInfo(path);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return new DiagnosticCheck(
                name,
                path,
                DiagnosticStatus.Information,
                $"{info.Length:N0} bytes; preservado como fonte legada e não alterado.");
        }
        catch (JsonException exception)
        {
            return new DiagnosticCheck(
                name,
                path,
                DiagnosticStatus.Error,
                $"JSON inválido; o arquivo foi preservado: {exception.Message}");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new DiagnosticCheck(
                name,
                path,
                DiagnosticStatus.Warning,
                $"Não foi possível consultar o arquivo: {exception.Message}");
        }
    }
}
