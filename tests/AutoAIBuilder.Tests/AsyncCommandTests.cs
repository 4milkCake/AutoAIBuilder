using AutoAIBuilder.Desktop.ViewModels;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class AsyncCommandTests
{
    [TestMethod]
    public async Task ExecuteAsync_PreventsReentrancyAndRestoresState()
    {
        var started = NewSignal();
        var release = NewSignal();
        var executionCount = 0;
        var command = new AsyncCommand(
            async _ =>
            {
                Interlocked.Increment(ref executionCount);
                started.TrySetResult();
                await release.Task;
            });

        var first = command.ExecuteAsync();
        await started.Task;
        await command.ExecuteAsync();

        Assert.IsTrue(command.IsRunning);
        Assert.IsFalse(command.CanExecute(null));
        Assert.AreEqual(1, executionCount);

        release.TrySetResult();
        await first;

        Assert.IsFalse(command.IsRunning);
        Assert.IsTrue(command.CanExecute(null));
        Assert.IsNull(command.LastException);
    }

    [TestMethod]
    public async Task Cancel_CancelsCooperativeExecutionWithoutLeakingException()
    {
        var started = NewSignal();
        var command = new AsyncCommand(
            async cancellationToken =>
            {
                started.TrySetResult();
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
            });

        var execution = command.ExecuteAsync();
        await started.Task;
        command.Cancel();
        await execution;

        Assert.IsTrue(command.WasCancelled);
        Assert.IsFalse(command.TimedOut);
        Assert.IsNull(command.LastException);
        Assert.IsFalse(command.IsRunning);
    }

    [TestMethod]
    public async Task Timeout_IsReportedAsControlledError()
    {
        Exception? reportedException = null;
        var command = new AsyncCommand(
            cancellationToken => Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken),
            onError: exception => reportedException = exception,
            timeout: TimeSpan.FromMilliseconds(80));

        await command.ExecuteAsync();

        Assert.IsTrue(command.TimedOut);
        Assert.IsFalse(command.WasCancelled);
        Assert.IsInstanceOfType<TimeoutException>(command.LastException);
        Assert.AreSame(command.LastException, reportedException);
        Assert.IsFalse(command.IsRunning);
    }

    [TestMethod]
    public async Task Failure_IsCapturedPerCommand()
    {
        Exception? reportedException = null;
        var expected = new InvalidOperationException("falha isolada");
        var command = new AsyncCommand(
            _ => Task.FromException(expected),
            onError: exception => reportedException = exception);

        await command.ExecuteAsync();

        Assert.AreSame(expected, command.LastException);
        Assert.AreSame(expected, reportedException);
        Assert.IsFalse(command.IsRunning);
        Assert.IsTrue(command.CanExecute(null));
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
