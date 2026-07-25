using System.Runtime.InteropServices;
using System.Text.Json;
using AutoAIBuilder.Application.Automation.Contracts;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Infrastructure.Persistence;

namespace AutoAIBuilder.Infrastructure.Diagnostics;

public sealed class EnvironmentDiagnosticService(
    IDiagnosticLogger logger,
    SqliteDatabase database,
    IAutomationAuditRepository automationAuditRepository) : IDiagnosticService
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
            CreateAutomationContractCheck(automationAuditRepository),
            CreatePathCheck("Backups de segurança", AppStoragePaths.BackupDirectory),
            CreateDiagnosticLogCheck(logger),
            CreateLegacyJsonCheck("Projetos legados", AppStoragePaths.ProjectsFile),
            CreateLegacyJsonCheck("Configurações legadas", AppStoragePaths.SettingsFile),
            CreateLegacyJsonCheck("Histórico legado", AppStoragePaths.ActivityLogFile)
        };

        return new DiagnosticSnapshot(
            DateTimeOffset.Now,
            checks,
            logger.GetRecent(100));
    }

    private static DiagnosticCheck CreateAutomationContractCheck(
        IAutomationAuditRepository repository)
    {
        try
        {
            var recent = repository.GetRecent(500);
            var completed = recent.Count(
                entry => entry.Status is AutomationAuditStatus.Succeeded
                    or AutomationAuditStatus.Reused
                    or AutomationAuditStatus.Simulated);
            return new DiagnosticCheck(
                "Contratos seguros de automação",
                $"Regras {AutomationContractVersions.RuleCatalogSchema}; "
                + $"máscaras {AutomationContractVersions.MaskSchema}",
                DiagnosticStatus.Healthy,
                "Planejamento, simulação, cópias isoladas, SHA-256, "
                + "pós-validação, recuperação e idempotência disponíveis. "
                + $"{recent.Count} auditoria(s), {completed} concluída(s). "
                + "Nenhuma máscara real foi instalada nesta etapa.");
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or InvalidOperationException)
        {
            return new DiagnosticCheck(
                "Contratos seguros de automação",
                "Auditoria indisponível",
                DiagnosticStatus.Error,
                $"Não foi possível consultar as auditorias: {exception.Message}");
        }
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

    private static DiagnosticCheck CreateDiagnosticLogCheck(
        IDiagnosticLogger logger)
    {
        if (logger is not JsonLinesDiagnosticLogger jsonLogger)
        {
            return CreatePathCheck("Log estruturado", logger.StoragePath);
        }

        try
        {
            var activeSize = File.Exists(jsonLogger.StoragePath)
                ? new FileInfo(jsonLogger.StoragePath).Length
                : 0;
            var retention = jsonLogger.Retention;
            return new DiagnosticCheck(
                "Log estruturado e retenção",
                jsonLogger.StoragePath,
                DiagnosticStatus.Healthy,
                $"{activeSize:N0} bytes no arquivo ativo; "
                + $"{jsonLogger.ArchiveCount}/{retention.MaximumArchiveFiles} "
                + "arquivo(s) anterior(es); rotação automática em "
                + $"{retention.MaximumFileSizeBytes:N0} bytes.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new DiagnosticCheck(
                "Log estruturado e retenção",
                jsonLogger.StoragePath,
                DiagnosticStatus.Warning,
                $"Não foi possível consultar a retenção: {exception.Message}");
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
