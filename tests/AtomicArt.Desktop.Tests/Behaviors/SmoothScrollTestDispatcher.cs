using Avalonia.Headless;

namespace AtomicArt.Desktop.Tests.Behaviors;

internal static class SmoothScrollTestDispatcher
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    internal static void Dispatch(Action action)
    {
        SessionLock.Wait();

        try
        {
            using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(SmoothScrollBehaviorTests));

            session.Dispatch(action, CancellationToken.None);
        }
        finally
        {
            SessionLock.Release();
        }
    }

    internal static async Task DispatchAsync(Func<Task> action)
    {
        await SessionLock.WaitAsync();

        try
        {
            await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(SmoothScrollBehaviorTests));

            await session.Dispatch(
                async () =>
                {
                    await action();

                    return true;
                },
                CancellationToken.None);
        }
        finally
        {
            SessionLock.Release();
        }
    }
}
