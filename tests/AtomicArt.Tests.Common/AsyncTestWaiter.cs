namespace AtomicArt.Tests.Common;

public static class AsyncTestWaiter
{
    private const int MaxAttempts = 500;
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(10);

    public static async Task WaitForConditionAsync(Func<bool> condition, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(condition);

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(Delay, ct).ConfigureAwait(false);
        }

        throw new TimeoutException("The expected asynchronous test condition was not reached.");
    }
}
