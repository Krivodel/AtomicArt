using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.SingleInstance;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.SingleInstance;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public async Task TryStartOrNotifyExisting_WithRunningPrimary_NotifiesAndRejectsSecondary()
    {
        string coordinationDirectory = CreateCoordinationDirectory();

        try
        {
            SingleInstanceIdentity identity = CreateIdentity(
                coordinationDirectory);
            using SingleInstanceCoordinator primary = CreateCoordinator(
                identity);
            using SingleInstanceCoordinator secondary = CreateCoordinator(
                identity);
            TaskCompletionSource activationSource = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            primary.TryStartOrNotifyExisting().Should().BeTrue();
            primary.AttachActivationHandler(() =>
            {
                activationSource.TrySetResult();

                return Task.CompletedTask;
            });

            bool secondaryStarted = await Task.Run(
                secondary.TryStartOrNotifyExisting);

            secondaryStarted.Should().BeFalse();
            await activationSource.Task.WaitAsync(TimeSpan.FromSeconds(5d));
        }
        finally
        {
            TestDirectories.DeleteIfExists(coordinationDirectory);
        }
    }

    [Fact]
    public async Task AttachActivationHandler_WithRequestDuringStartup_DeliversPendingRequest()
    {
        string coordinationDirectory = CreateCoordinationDirectory();

        try
        {
            SingleInstanceIdentity identity = CreateIdentity(
                coordinationDirectory);
            using SingleInstanceCoordinator primary = CreateCoordinator(
                identity);
            using SingleInstanceCoordinator secondary = CreateCoordinator(
                identity);
            TaskCompletionSource activationSource = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            primary.TryStartOrNotifyExisting().Should().BeTrue();
            Task<bool> secondaryStartTask = Task.Run(
                secondary.TryStartOrNotifyExisting);
            await Task.Delay(100);

            primary.AttachActivationHandler(() =>
            {
                activationSource.TrySetResult();

                return Task.CompletedTask;
            });

            (await secondaryStartTask).Should().BeFalse();
            await activationSource.Task.WaitAsync(TimeSpan.FromSeconds(5d));
        }
        finally
        {
            TestDirectories.DeleteIfExists(coordinationDirectory);
        }
    }

    [Fact]
    public void Dispose_WhenPrimaryStops_ReleasesInstanceOwnership()
    {
        string coordinationDirectory = CreateCoordinationDirectory();

        try
        {
            SingleInstanceIdentity identity = CreateIdentity(
                coordinationDirectory);
            SingleInstanceCoordinator primary = CreateCoordinator(identity);
            using SingleInstanceCoordinator replacement = CreateCoordinator(
                identity);
            primary.TryStartOrNotifyExisting().Should().BeTrue();

            primary.Dispose();

            replacement.TryStartOrNotifyExisting().Should().BeTrue();
        }
        finally
        {
            TestDirectories.DeleteIfExists(coordinationDirectory);
        }
    }

    private static string CreateCoordinationDirectory()
    {
        return TestDirectories.GetUniqueDirectoryPath(
            typeof(SingleInstanceCoordinatorTests));
    }

    private static SingleInstanceIdentity CreateIdentity(
        string coordinationDirectory)
    {
        string identitySuffix = Guid.NewGuid().ToString("N");

        return new SingleInstanceIdentity(
            Path.Combine(
                coordinationDirectory,
                identitySuffix + ".lock"),
            "AtomicArt-Tests-" + identitySuffix);
    }

    private static SingleInstanceCoordinator CreateCoordinator(
        SingleInstanceIdentity identity)
    {
        return new SingleInstanceCoordinator(
            identity,
            NullLogger<SingleInstanceCoordinator>.Instance);
    }
}
