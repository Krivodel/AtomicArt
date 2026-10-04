using Microsoft.Extensions.DependencyInjection;

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Tests.Common;
using Pica.Protocol;
using Pica.Viewer;
using Pica.Viewer.Services;
using Pica.Viewer.Views;

namespace AtomicArt.Desktop.Tests.Styles;

public sealed class PicaViewerStylesTests : DesktopControlTestBase
{
    [Fact]
    public async Task SettingsIcon_WithApplicationStyles_KeepsNormalTitleBarSize()
    {
        await DispatchAsync(async () =>
        {
            const double SettingsIconSize = 16d;
            Mock<IImageViewerStateService> stateService = new();
            stateService.Setup(service => service.LoadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ImageViewerState
                {
                    IsWindowed = true,
                    RememberWindowPlacement = true,
                    AutoHideWindowTitleBar = false,
                    WindowWidth = 640d,
                    WindowHeight = 480d
                });
            ServiceCollection services = new();
            services.AddLogging();
            services.AddPicaViewer();
            services.AddSingleton<IImageViewerStateService>(stateService.Object);
            using ServiceProvider provider = services.BuildServiceProvider();
            IImageViewerWindowFactory factory = provider.GetRequiredService<IImageViewerWindowFactory>();
            PicaViewerRequest request = new(Array.Empty<PicaImageItem>(), Guid.Empty);
            ImageViewerWindow window = await factory.CreateAsync(request, CancellationToken.None);

            try
            {
                window.Show();
                using Bitmap? frame = window.CaptureRenderedFrame();
                Dispatcher.CurrentDispatcher.RunJobs();
                window.UpdateLayout();
                Button button = window.RightWindowTitleBarControls.Single().Should().BeOfType<Button>().Subject;
                PathIcon icon = button.Content.Should().BeOfType<PathIcon>().Subject;

                icon.Bounds.Size.Should().Be(new Size(SettingsIconSize, SettingsIconSize));
            }
            finally
            {
                Dispatcher.CurrentDispatcher.RunJobs();
                window.Close();
            }
        });
    }
}
