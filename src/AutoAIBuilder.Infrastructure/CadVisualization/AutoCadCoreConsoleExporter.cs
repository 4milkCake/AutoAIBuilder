using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AutoAIBuilder.Infrastructure.CadVisualization;

public sealed class AutoCadCoreConsoleExporter : ICadGeometryExporter
{
    private const string ResourceName =
        "AutoAIBuilder.Infrastructure.CadVisualization."
        + "AutoCadVisualizationExport.lsp";
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(3);

    private readonly string? _configuredExecutablePath;

    public AutoCadCoreConsoleExporter(string? executablePath = null)
    {
        _configuredExecutablePath = executablePath;
    }

    public CadExporterStatus GetStatus()
    {
        var path = ResolveExecutablePath();
        if (path is null)
        {
            return new CadExporterStatus(
                false,
                "AutoCAD Core Console",
                "Não encontrado",
                string.Empty,
                string.Empty,
                "O AutoCAD Core Console não foi localizado.");
        }

        var version = FileVersionInfo.GetVersionInfo(path);
        var publisher = GetPublisher(path);
        var isAutodesk = publisher.Contains(
                "Autodesk",
                StringComparison.OrdinalIgnoreCase)
            || (version.CompanyName?.Contains(
                "Autodesk",
                StringComparison.OrdinalIgnoreCase) ?? false);
        return new CadExporterStatus(
            isAutodesk,
            "AutoCAD Core Console",
            version.FileVersion ?? version.ProductVersion ?? "Desconhecida",
            path,
            publisher,
            isAutodesk
                ? "Motor gráfico oficial Autodesk disponível."
                : "O executável encontrado não pôde ser confirmado como Autodesk.");
    }

    public async Task ExportAsync(
        string sourceCopyPath,
        string artifactPath,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var status = GetStatus();
        if (!status.IsAvailable)
        {
            throw new InvalidOperationException(status.Message);
        }

        Directory.CreateDirectory(workingDirectory);
        var lispPath = Path.Combine(
            workingDirectory,
            "autoaibuilder-visual-export.lsp");
        var scriptPath = Path.Combine(
            workingDirectory,
            "autoaibuilder-visual-export.scr");
        ExtractLispResource(lispPath);
        await File.WriteAllTextAsync(
                scriptPath,
                BuildScript(lispPath, artifactPath),
                cancellationToken)
            .ConfigureAwait(false);

        var startInfo = new ProcessStartInfo
        {
            FileName = status.ExecutablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("/i");
        startInfo.ArgumentList.Add(sourceCopyPath);
        startInfo.ArgumentList.Add("/s");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("/l");
        startInfo.ArgumentList.Add("en-US");

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException(
                "O AutoCAD Core Console não pôde ser iniciado.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = new CancellationTokenSource(ProcessTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryTerminate(process);
            if (timeout.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    "O AutoCAD excedeu três minutos ao gerar a visualização.");
            }

            throw;
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        await File.WriteAllTextAsync(
                Path.Combine(workingDirectory, "core-console.log"),
                output + Environment.NewLine + error,
                CancellationToken.None)
            .ConfigureAwait(false);

        if (process.ExitCode != 0 || !File.Exists(artifactPath))
        {
            throw new InvalidOperationException(
                "O AutoCAD não concluiu a exportação gráfica. "
                + $"Código {process.ExitCode}. Consulte core-console.log.");
        }
    }

    private string? ResolveExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_configuredExecutablePath)
            && File.Exists(_configuredExecutablePath))
        {
            return Path.GetFullPath(_configuredExecutablePath);
        }

        var candidates = new[]
        {
            @"D:\Autodesk\AutoCAD 2025\accoreconsole.exe",
            @"C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe",
            @"C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe",
            @"C:\Program Files\Autodesk\AutoCAD 2027\accoreconsole.exe"
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static void ExtractLispResource(string destinationPath)
    {
        using var source = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                "O exportador AutoLISP incorporado não foi encontrado.");
        using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        source.CopyTo(destination);
    }

    private static string BuildScript(
        string lispPath,
        string artifactPath)
    {
        static string LispPath(string path) =>
            path.Replace('\\', '/').Replace("\"", "\\\"");

        return "(setvar \"FILEDIA\" 0)\r\n"
            + "(setvar \"CMDDIA\" 0)\r\n"
            + "(setvar \"SECURELOAD\" 0)\r\n"
            + $"(load \"{LispPath(lispPath)}\")\r\n"
            + $"(aiv:export \"{LispPath(artifactPath)}\")\r\n"
            + "(princ)\r\n";
    }

    private static string GetPublisher(string path)
    {
        try
        {
            using var certificate = new X509Certificate2(
                X509Certificate.CreateFromSignedFile(path));
            return certificate.Subject;
        }
        catch (Exception exception) when (
            exception is CryptographicException
                or InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // O processo terminou entre as verificações.
        }
    }
}
