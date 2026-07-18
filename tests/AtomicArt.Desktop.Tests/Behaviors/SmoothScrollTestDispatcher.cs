using AtomicArt.Tests.Avalonia;

namespace AtomicArt.Desktop.Tests.Behaviors;

internal static class SmoothScrollTestDispatcher
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    internal static void Dispatch(Action action)
    {
        HeadlessTestSessionDispatcher.Dispatch(
            typeof(SmoothScrollBehaviorTests),
            SessionLock,
            action);
    }

    internal static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(SmoothScrollBehaviorTests),
            SessionLock,
            action);
    }
}
