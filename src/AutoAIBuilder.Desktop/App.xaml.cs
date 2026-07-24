using System.Windows;
using System.Windows.Threading;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Desktop.Composition;
using AutoAIBuilder.Desktop.Services;

namespace AutoAIBuilder.Desktop;

public partial class App : System.Windows.Application
{
    private SingleInstanceGuard? _singleInstanceGuard;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        _singleInstanceGuard = SingleInstanceGuard.TryAcquire();
        if (_singleInstanceGuard is null)
        {
            MessageBox.Show(
                "O AutoAIBuilder já está aberto nesta sessão do Windows. "
                + "Use a instância existente para evitar gravações concorrentes.",
                "AutoAIBuilder já está em execução",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        if (eventArgs.Args.Any(argument =>
                string.Equals(
                    argument,
                    "--migrate-data-only",
                    StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var database = DesktopCompositionRoot.InitializeDataStore();
                TryLog(
                    DiagnosticLevel.Information,
                    "DataMigration",
                    $"Migração de dados concluída. Esquema {database.GetSchemaVersion()}.");
                Shutdown(0);
            }
            catch (Exception exception)
            {
                TryLog(
                    DiagnosticLevel.Critical,
                    "DataMigration",
                    "A migração de dados em modo de manutenção falhou.",
                    exception);
                Shutdown(1);
            }

            return;
        }

        TryLog(
            DiagnosticLevel.Information,
            "Lifecycle",
            "Aplicação desktop iniciada.");
        base.OnStartup(eventArgs);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        TryLog(
            DiagnosticLevel.Information,
            "Lifecycle",
            $"Aplicação desktop encerrada com código {eventArgs.ApplicationExitCode}.");
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        _singleInstanceGuard?.Dispose();
        _singleInstanceGuard = null;
        base.OnExit(eventArgs);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        TryLog(
            DiagnosticLevel.Critical,
            "Dispatcher",
            "Exceção não tratada na interface.",
            eventArgs.Exception);
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs eventArgs)
    {
        TryLog(
            DiagnosticLevel.Error,
            "TaskScheduler",
            "Exceção assíncrona não observada.",
            eventArgs.Exception);
        eventArgs.SetObserved();
    }

    private static void TryLog(
        DiagnosticLevel level,
        string source,
        string message,
        Exception? exception = null)
    {
        try
        {
            DesktopCompositionRoot.DiagnosticLogger.Write(
                level,
                source,
                message,
                exception);
        }
        catch
        {
            // O logging nunca deve impedir inicialização ou encerramento.
        }
    }
}
