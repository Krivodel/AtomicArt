using Moq;
using Xunit;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class TrayServiceTests
{
    [Fact]
    public void HideToTray_HidesWindowThroughWindowStateService()
    {
        Mock<IWindowStateService> windowStateService = new(
            MockBehavior.Strict);
        windowStateService
            .Setup(service => service.Hide());
        using TrayService trayService = new(windowStateService.Object);

        trayService.HideToTray();

        windowStateService.Verify(
            service => service.Hide(),
            Times.Once);
    }
}
