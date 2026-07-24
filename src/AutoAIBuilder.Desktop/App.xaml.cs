using System.Windows;
using System.Windows.Threading;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Desktop.Composition;

namespace AutoAIBuilder.Desktop;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        TryLog(
            DiagnosticLevel.Information,
            "Lifecycle",
            "Aplicação desktop iniciada.");
        base.OnStartup(eventArgs);
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        TryLog(
            DiagnosticLevel.Information,
            "Lifecycle",
            $"Aplicação desktop encerrada com código {eventArgs.ApplicationExitCode}.");
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
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
