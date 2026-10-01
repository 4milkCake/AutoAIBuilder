using System.Diagnostics;
using System.Text;
using AutoAIBuilder.Infrastructure.CadVisualization;

namespace AutoAIBuilder.Infrastructure.Automation.Supervised;

public sealed record SupervisedCadAssignment(
    string Handle,
    string Layer,
    string Kind);

public sealed record SupervisedCadRunnerRequest(
    string WorkingDwgPath,
    string ScriptsDirectory,
    string RunDirectory,
    IReadOnlyList<SupervisedCadAssignment> Assignments);

public sealed record SupervisedCadRunnerResult(
    string ReportPath,
    string PreservationReportPath,
    string LogPath);

public interface ISupervisedCadRunner
{
    CadExporterStatus GetStatus();

    Task<SupervisedCadRunnerResult> RunAsync(
        SupervisedCadRunnerRequest request,
        CancellationToken cancellationToken);
}

public sealed class AutoCadSupervisedRunner(
    AutoCadCoreConsoleExporter? engine = null) : ISupervisedCadRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(8);
    private static readonly string[] HistoricalScripts =
    [
        "mascara_previsualizar_v04.lsp",
        "mascara_camadas_v05.lsp",
        "mascara_limpeza_v06.lsp"
    ];

    private readonly AutoCadCoreConsoleExporter _engine =
        engine ?? new AutoCadCoreConsoleExporter();

    public CadExporterStatus GetStatus() => _engine.GetStatus();

    public async Task<SupervisedCadRunnerResult> RunAsync(
        SupervisedCadRunnerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var status = GetStatus();
        if (!status.IsAvailable)
        {
            throw new InvalidOperationException(status.Message);
        }

        var originals = Path.Combine(request.RunDirectory, "rotinas-originais");
        var adapters = Path.Combine(request.RunDirectory, "adaptadores");
        Directory.CreateDirectory(originals);
        Directory.CreateDirectory(adapters);
        foreach (var scriptName in HistoricalScripts)
        {
            var source = RequireScript(request.ScriptsDirectory, scriptName);
            File.Copy(
                source,
                Path.Combine(originals, scriptName),
                overwrite: false);
        }

        var reportPath = Path.Combine(
            request.RunDirectory,
            "resultado-entidades.csv");
        var preservationReportPath = Path.Combine(
            request.RunDirectory,
            "resultado-preservacao.csv");
        var adapterPath = Path.Combine(
            adapters,
            "autoaibuilder-preservacao-11.6h1.lsp");
        var scriptPath = Path.Combine(
            request.RunDirectory,
            "executar-11.6h1.scr");
        var logPath = Path.Combine(
            request.RunDirectory,
            "autoCAD-core-console.log");

        var adapter = BuildPreservationExecutor(
            request.Assignments,
            reportPath,
            preservationReportPath);
        if (adapter.Contains(
                "entdel",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "O adaptador de preservação contém uma operação de exclusão.");
        }

        await File.WriteAllTextAsync(
                adapterPath,
                adapter,
                Encoding.ASCII,
                cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                scriptPath,
                BuildScript(adapterPath),
                Encoding.ASCII,
                cancellationToken)
            .ConfigureAwait(false);

        var startInfo = new ProcessStartInfo
        {
            FileName = status.ExecutablePath,
            WorkingDirectory = request.RunDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("/i");
        startInfo.ArgumentList.Add(request.WorkingDwgPath);
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

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = new CancellationTokenSource(Timeout);
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
                    "A execução supervisionada excedeu oito minutos.");
            }

            throw;
        }

        var log = await stdout.ConfigureAwait(false)
                  + Environment.NewLine
                  + await stderr.ConfigureAwait(false);
        await File.WriteAllTextAsync(
                logPath,
                log,
                Encoding.UTF8,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (process.ExitCode != 0
            || !File.Exists(reportPath)
            || !File.Exists(preservationReportPath))
        {
            throw new InvalidOperationException(
                "O AutoCAD não concluiu o executor de preservação 11.6H.1. "
                + $"Código {process.ExitCode}; consulte {logPath}.");
        }

        return new SupervisedCadRunnerResult(
            reportPath,
            preservationReportPath,
            logPath);
    }

    private static string RequireScript(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"A rotina obrigatória {fileName} não foi encontrada.",
                path);
        }

        return path;
    }

    private static string BuildPreservationExecutor(
        IReadOnlyList<SupervisedCadAssignment> assignments,
        string reportPath,
        string preservationReportPath)
    {
        static string Atom(string value) =>
            $"\"{value.Replace("\"", string.Empty)}\"";
        static string LispPath(string path) =>
            path.Replace('\\', '/').Replace("\"", string.Empty);

        var assignmentList = string.Join(
            "\r\n    ",
            assignments
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.Handle)
                    && !string.IsNullOrWhiteSpace(item.Layer))
                .GroupBy(
                    item => item.Handle,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(item =>
                    $"({Atom(item.Handle)} {Atom(item.Layer)} {Atom(item.Kind)})"));

        return
            $"(setq *aab:assignments* '(\r\n    {assignmentList}))\r\n"
            + "(defun aab:csv (value)\r\n"
            + "  (strcat \"\\\"\" (if value value \"\") \"\\\"\"))\r\n"
            + "(defun aab:model-handles (/ selection index entity data result)\r\n"
            + "  (setq result '() selection (ssget \"_X\" '((410 . \"Model\"))))\r\n"
            + "  (if selection\r\n"
            + "    (progn (setq index 0)\r\n"
            + "      (repeat (sslength selection)\r\n"
            + "        (setq entity (ssname selection index))\r\n"
            + "        (setq data (entget entity))\r\n"
            + "        (setq result (cons (cdr (assoc 5 data)) result))\r\n"
            + "        (setq index (1+ index)))))\r\n"
            + "  result)\r\n"
            + "(defun aab:count-named-block (name / selection index data count block)\r\n"
            + "  (setq count 0 selection (ssget \"_X\" '((0 . \"INSERT\") (410 . \"Model\"))))\r\n"
            + "  (if selection\r\n"
            + "    (progn (setq index 0)\r\n"
            + "      (repeat (sslength selection)\r\n"
            + "        (setq data (entget (ssname selection index)))\r\n"
            + "        (setq block (cdr (assoc 2 data)))\r\n"
            + "        (if (and block (= (strcase block) (strcase name)))\r\n"
            + "          (setq count (1+ count)))\r\n"
            + "        (setq index (1+ index)))))\r\n"
            + "  count)\r\n"
            + "(defun aab:missing-count (before after / count handle)\r\n"
            + "  (setq count 0)\r\n"
            + "  (foreach handle before\r\n"
            + "    (if (not (member handle after)) (setq count (1+ count))))\r\n"
            + "  count)\r\n"
            + "(defun aab:ensure-layer (name / result)\r\n"
            + "  (if (not (tblsearch \"LAYER\" name))\r\n"
            + "    (setq result\r\n"
            + "      (entmake\r\n"
            + "        (list '(0 . \"LAYER\") '(100 . \"AcDbSymbolTableRecord\")\r\n"
            + "          '(100 . \"AcDbLayerTableRecord\") (cons 2 name)\r\n"
            + "          '(70 . 0) '(62 . 7) '(6 . \"Continuous\")))))\r\n"
            + "  (or (tblsearch \"LAYER\" name) result))\r\n"
            + "(defun aab:set-layer-only (handle layer / entity data pair result)\r\n"
            + "  (setq entity (handent handle))\r\n"
            + "  (if (null entity) \"AUSENTE\"\r\n"
            + "    (progn\r\n"
            + "      (aab:ensure-layer layer)\r\n"
            + "      (setq data (entget entity))\r\n"
            + "      (setq pair (assoc 8 data))\r\n"
            + "      (setq result (entmod (subst (cons 8 layer) pair data)))\r\n"
            + "      (if result \"APLICADO\" \"ERRO\"))))\r\n"
            + "(defun aab:write-entity-report (path / file item entity data layer status)\r\n"
            + "  (setq file (open path \"w\"))\r\n"
            + "  (if file\r\n"
            + "    (progn\r\n"
            + "      (write-line \"\\\"HANDLE\\\";\\\"LAYER\\\";\\\"KIND\\\";\\\"STATUS\\\"\" file)\r\n"
            + "      (foreach item *aab:assignments*\r\n"
            + "        (setq entity (handent (nth 0 item)))\r\n"
            + "        (if entity\r\n"
            + "          (progn (setq data (entget entity))\r\n"
            + "                 (setq layer (cdr (assoc 8 data)))\r\n"
            + "                 (setq status (if (= (strcase layer) (strcase (nth 1 item))) \"OK\" \"DIVERGENTE\")))\r\n"
            + "          (progn (setq layer \"\") (setq status \"AUSENTE\")))\r\n"
            + "        (write-line (strcat (aab:csv (nth 0 item)) \";\"\r\n"
            + "          (aab:csv layer) \";\" (aab:csv (nth 2 item)) \";\"\r\n"
            + "          (aab:csv status)) file))\r\n"
            + "      (close file))))\r\n"
            + "(defun aab:write-preservation-report (path before after block-before block-after / file missing)\r\n"
            + "  (setq missing (aab:missing-count before after))\r\n"
            + "  (setq file (open path \"w\"))\r\n"
            + "  (if file\r\n"
            + "    (progn\r\n"
            + "      (write-line \"\\\"ENTIDADES_ANTES\\\";\\\"ENTIDADES_DEPOIS\\\";\\\"HANDLES_AUSENTES\\\";\\\"CINZA_PONTOS_ANTES\\\";\\\"CINZA_PONTOS_DEPOIS\\\"\" file)\r\n"
            + "      (write-line (strcat (aab:csv (itoa (length before))) \";\"\r\n"
            + "        (aab:csv (itoa (length after))) \";\"\r\n"
            + "        (aab:csv (itoa missing)) \";\"\r\n"
            + "        (aab:csv (itoa block-before)) \";\"\r\n"
            + "        (aab:csv (itoa block-after))) file)\r\n"
            + "      (close file))))\r\n"
            + "(defun c:AAB_EXECUTAR_116H1 (/ item before after block-before block-after)\r\n"
            + "  (setq before (aab:model-handles))\r\n"
            + "  (setq block-before (aab:count-named-block \"CINZA PONTOS\"))\r\n"
            + "  (foreach item *aab:assignments*\r\n"
            + "    (aab:set-layer-only (nth 0 item) (nth 1 item)))\r\n"
            + "  (setq after (aab:model-handles))\r\n"
            + "  (setq block-after (aab:count-named-block \"CINZA PONTOS\"))\r\n"
            + $"  (aab:write-entity-report \"{LispPath(reportPath)}\")\r\n"
            + $"  (aab:write-preservation-report \"{LispPath(preservationReportPath)}\" before after block-before block-after)\r\n"
            + "  (princ))\r\n";
    }

    private static string BuildScript(string adapterPath)
    {
        var path = adapterPath
            .Replace('\\', '/')
            .Replace("\"", string.Empty);
        return "(setvar \"FILEDIA\" 0)\r\n"
               + "(setvar \"CMDDIA\" 0)\r\n"
               + "(setvar \"SECURELOAD\" 0)\r\n"
               + $"(load \"{path}\")\r\n"
               + "(c:AAB_EXECUTAR_116H1)\r\n"
               + "(command \"_.QSAVE\")\r\n"
               + "(princ)\r\n";
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
            // O processo encerrou entre as verificações.
        }
    }
}
