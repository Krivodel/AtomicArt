using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Services.Updates;

namespace AtomicArt.Desktop.Tests.Services.Updates;

public sealed class ApplicationUpdateRestartCoordinatorTests
{
    [Fact]
    public async Task ApplyAndRestartAsync_WithAttachedTarget_FlushesStateBeforeApplyingUpdate()
    {
        int operationOrder = 0;
        Mock<IApplicationStateFlushService> stateFlushServiceMock = new();
        stateFlushServiceMock
            .Setup(service => service.FlushAsync(
                It.IsAny<IAppStateFlushTarget>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => operationOrder.Should().Be(0))
            .Returns(() =>
            {
                operationOrder++;
                return Task.CompletedTask;
            });
        Mock<IApplicationUpdateService> updateServiceMock = new();
        updateServiceMock
            .Setup(service => service.ApplyUpdateAndRestart(It.IsAny<ApplicationUpdate>()))
            .Callback(() =>
            {
                operationOrder.Should().Be(1);
                operationOrder++;
            });
        ApplicationUpdateRestartCoordinator coordinator = new(
            stateFlushServiceMock.Object,
            updateServiceMock.Object);
        Mock<IAppStateFlushTarget> stateFlushTargetMock = new();
        ApplicationUpdate update = new("1.2.3");
        coordinator.Attach(stateFlushTargetMock.Object);

        await coordinator.ApplyAndRestartAsync(update, CancellationToken.None);

        operationOrder.Should().Be(2);
    }

    [Fact]
    public async Task ApplyAndRestartAsync_WithoutAttachedTarget_ThrowsInvalidOperationException()
    {
        ApplicationUpdateRestartCoordinator coordinator = new(
            Mock.Of<IApplicationStateFlushService>(),
            Mock.Of<IApplicationUpdateService>());
        ApplicationUpdate update = new("1.2.3");

        Func<Task> act = () => coordinator.ApplyAndRestartAsync(
            update,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
