using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Notifications;

namespace AtomicArt.Desktop.Tests.Services.Notifications;

public sealed class NotificationHoverGroupTests
{
    [Fact]
    public async Task SetHovered_WhenAnyNotificationIsHovered_RestartsAllTimersAfterLeaving()
    {
        NotificationTestClock clock = new();
        NotificationHoverGroup group = new(clock.Time);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        Task<long> first = group.WaitForExpirationAsync(timeout.Token);
        Task<long> second = group.WaitForExpirationAsync(timeout.Token);
        Guid hovered = Guid.NewGuid();
        NotificationTestTimer[] oldTimers = clock.Timers.ToArray();

        group.SetHovered(hovered, true);
        await Task.WhenAll(oldTimers.Select(timer => timer.Disposed)).WaitAsync(timeout.Token);
        foreach (NotificationTestTimer timer in oldTimers)
        {
            timer.Fire();
        }

        first.IsCompleted.Should().BeFalse();
        second.IsCompleted.Should().BeFalse();
        group.SetHovered(hovered, false);
        await clock.WaitForTimerCountAsync(4, timeout.Token);
        NotificationTestTimer[] fresh = clock.Timers.Skip(2).ToArray();
        fresh.Should().OnlyContain(timer => timer.DueTime == TimeSpan.FromSeconds(5));
        foreach (NotificationTestTimer timer in fresh)
        {
            timer.Fire();
        }

        group.CanExpire(await first.WaitAsync(timeout.Token)).Should().BeTrue();
        group.CanExpire(await second.WaitAsync(timeout.Token)).Should().BeTrue();
    }

    [Fact]
    public async Task SetHovered_WhenMovingBetweenNotifications_KeepsTheTimersPaused()
    {
        NotificationTestClock clock = new();
        NotificationHoverGroup group = new(clock.Time);
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        group.SetHovered(first, true);
        Task<long> waiting = group.WaitForExpirationAsync(timeout.Token);

        group.SetHovered(second, true);
        group.SetHovered(first, false);

        clock.Timers.Should().BeEmpty();
        waiting.IsCompleted.Should().BeFalse();
        group.SetHovered(second, false);
        await clock.WaitForTimerCountAsync(1, timeout.Token);
        clock.Timers.Single().Fire();
        group.CanExpire(await waiting.WaitAsync(timeout.Token)).Should().BeTrue();
    }

    [Fact]
    public async Task CanExpire_WhenHoverArrivesAfterTheTimerCallback_RejectsTheStaleExpiration()
    {
        NotificationTestClock clock = new();
        NotificationHoverGroup group = new(clock.Time);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        Task<long> waiting = group.WaitForExpirationAsync(timeout.Token);
        clock.Timers.Single().Fire();
        long version = await waiting.WaitAsync(timeout.Token);

        group.SetHovered(Guid.NewGuid(), true);

        group.CanExpire(version).Should().BeFalse();
    }
}
