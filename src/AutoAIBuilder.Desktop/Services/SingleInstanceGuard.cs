namespace AutoAIBuilder.Desktop.Services;

public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName =
        @"Local\AutoAIBuilder.Desktop.6F9D93D4-6A53-4D42-9A6F-D6F22E0A8A9E";

    private readonly Mutex _mutex;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static SingleInstanceGuard? TryAcquire()
    {
        var mutex = new Mutex(
            initiallyOwned: true,
            MutexName,
            out var createdNew);

        if (createdNew)
        {
            return new SingleInstanceGuard(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _mutex.ReleaseMutex();
        _mutex.Dispose();
        _disposed = true;
    }
}
