using System.Runtime.InteropServices;
using System.Text.Json;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Infrastructure.Persistence;

namespace AutoAIBuilder.Infrastructure.Diagnostics;

public sealed class EnvironmentDiagnosticService(
    IDiagnosticLogger logger) : IDiagnosticService
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
            CreatePathCheck("Log estruturado", logger.StoragePath),
            CreateJsonFileCheck("Projetos", AppStoragePaths.ProjectsFile),
            CreateJsonFileCheck("Configurações", AppStoragePaths.SettingsFile),
            CreateJsonFileCheck("Histórico", AppStoragePaths.ActivityLogFile)
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

    private static DiagnosticCheck CreateJsonFileCheck(string name, string path)
    {
        if (!File.Exists(path))
        {
            return new DiagnosticCheck(
                name,
                path,
                DiagnosticStatus.Information,
                "Arquivo ainda não criado ou não necessário.");
        }

        try
        {
            var info = new FileInfo(path);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return new DiagnosticCheck(
                name,
                path,
                DiagnosticStatus.Healthy,
                $"{info.Length:N0} bytes; atualizado em {info.LastWriteTime:dd/MM/yyyy HH:mm:ss}.");
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
