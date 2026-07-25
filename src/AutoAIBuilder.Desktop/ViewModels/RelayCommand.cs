using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AutoAIBuilder.Desktop.ViewModels;

internal sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class RelayCommand<T>(
    Action<T?> execute,
    Predicate<T?>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        canExecute?.Invoke(ConvertParameter(parameter)) ?? true;

    public void Execute(object? parameter) =>
        execute(ConvertParameter(parameter));

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? ConvertParameter(object? parameter) =>
        parameter is T value ? value : default;
}

public sealed class AsyncCommand : ICommand, INotifyPropertyChanged
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onError;
    private readonly TimeSpan? _timeout;
    private readonly object _sync = new();
    private CancellationTokenSource? _userCancellation;
    private bool _isRunning;
    private bool _wasCancelled;
    private bool _timedOut;
    private Exception? _lastException;

    public AsyncCommand(
        Func<CancellationToken, Task> execute,
        Func<bool>? canExecute = null,
        Action<Exception>? onError = null,
        TimeSpan? timeout = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onError = onError;

        if (timeout is not null && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "O timeout deve ser maior que zero.");
        }

        _timeout = timeout;
    }

    public event EventHandler? CanExecuteChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanCancel));
            RaiseCanExecuteChanged();
        }
    }

    public bool CanCancel => IsRunning;

    public bool WasCancelled
    {
        get => _wasCancelled;
        private set
        {
            if (_wasCancelled == value)
            {
                return;
            }

            _wasCancelled = value;
            OnPropertyChanged();
        }
    }

    public bool TimedOut
    {
        get => _timedOut;
        private set
        {
            if (_timedOut == value)
            {
                return;
            }

            _timedOut = value;
            OnPropertyChanged();
        }
    }

    public Exception? LastException
    {
        get => _lastException;
        private set
        {
            if (ReferenceEquals(_lastException, value))
            {
                return;
            }

            _lastException = value;
            OnPropertyChanged();
        }
    }

    public bool CanExecute(object? parameter)
    {
        lock (_sync)
        {
            return !_isRunning && (_canExecute?.Invoke() ?? true);
        }
    }

    public void Execute(object? parameter) => _ = ExecuteAsync();

    public async Task ExecuteAsync()
    {
        CancellationTokenSource userCancellation;
        lock (_sync)
        {
            if (_isRunning || !(_canExecute?.Invoke() ?? true))
            {
                return;
            }

            userCancellation = new CancellationTokenSource();
            _userCancellation = userCancellation;
            WasCancelled = false;
            TimedOut = false;
            LastException = null;
            IsRunning = true;
        }

        using var timeoutCancellation = _timeout is null
            ? null
            : new CancellationTokenSource(_timeout.Value);
        using var linkedCancellation = timeoutCancellation is null
            ? CancellationTokenSource.CreateLinkedTokenSource(
                userCancellation.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(
                userCancellation.Token,
                timeoutCancellation.Token);

        try
        {
            await _execute(linkedCancellation.Token);
        }
        catch (OperationCanceledException)
            when (timeoutCancellation?.IsCancellationRequested == true
                  && !userCancellation.IsCancellationRequested)
        {
            TimedOut = true;
            var exception = new TimeoutException(
                $"A operação excedeu o tempo limite de {_timeout}.");
            LastException = exception;
            TryReportError(exception);
        }
        catch (OperationCanceledException)
            when (userCancellation.IsCancellationRequested)
        {
            WasCancelled = true;
        }
        catch (Exception exception)
        {
            LastException = exception;
            TryReportError(exception);
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_userCancellation, userCancellation))
                {
                    _userCancellation = null;
                }

                IsRunning = false;
            }

            userCancellation.Dispose();
        }
    }

    public void Cancel()
    {
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            cancellation = _userCancellation;
        }

        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A execução terminou entre a consulta e a solicitação.
        }
    }

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private void TryReportError(Exception exception)
    {
        try
        {
            _onError?.Invoke(exception);
        }
        catch
        {
            // O tratamento visual de erro não pode escapar do comando.
        }
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
}
