using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Moq;
using Pica.Protocol;
using Pica.Viewer;
using Pica.Viewer.Services;
using Pica.Viewer.Views;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class ImageViewerWindowLimitTests : DesktopControlTestBase
{
    [Fact]
    public async Task ExitApplication_WithOpenViewers_CompletesViewerCleanupBeforeShutdown()
    {
        await DispatchAsync(async () =>
        {
            await using ViewerContext context = new(ImageViewerWindowLimitSettingDefinition.Unlimited);
            await context.OpenAsync();
            await context.OpenAsync(temporary: false);
            using TrayService trayService = new(
                Mock.Of<IWindowStateService>(),
                TestLocalizationTextProvider.Default,
                new WeakReferenceMessenger());
            Mock<IClassicDesktopStyleApplicationLifetime> lifetime = new();
            ShutdownRequestedEventArgs request = new();
            TaskCompletionSource<bool> shutdown = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lifetime.Setup(target => target.TryShutdown(0)).Returns(() =>
            {
                lifetime.Raise(target => target.ShutdownRequested += null, lifetime.Object, request);
                return !request.Cancel;
            });
            lifetime.Setup(target => target.Shutdown(0)).Callback(() => shutdown.TrySetResult(
                context.Windows.All(window => !window.IsVisible && window.CloseCleanupCompletion.IsCompletedSuccessfully)
                && !File.Exists(context.ImagePaths[0])));
            App app = new();
            app.AttachDesktopShutdownHandlers(lifetime.Object, context.Service);
            trayService.Attach(lifetime.Object);

            trayService.HideToTray();
            context.Windows.Should().OnlyContain(window => window.IsVisible);

            trayService.ExitApplication();

            bool viewerCleanupCompleted = await shutdown.Task.WaitAsync(TimeSpan.FromSeconds(3d));
            request.Cancel.Should().BeTrue();
            viewerCleanupCompleted.Should().BeTrue();
            File.Exists(context.ImagePaths[1]).Should().BeTrue();
        });
    }

    [Fact]
    public async Task OpenAsync_WithSavedAttachment_ClosingViewerKeepsAttachmentFile()
    {
        await DispatchAsync(async () =>
        {
            await using ViewerContext context = new(1);
            await context.OpenAsync(temporary: false);
            ImageViewerWindow window = context.Windows.Single();
            window.CaptureRenderedFrame();

            window.Close();
            await window.CloseCleanupCompletion;

            File.Exists(context.ImagePaths.Single()).Should().BeTrue();
        });
    }

    [Fact]
    public async Task OpenAsync_WhenLimitIsReached_ClosesOldestEvenIfItWasActivated()
    {
        await DispatchAsync(async () =>
        {
            await using ViewerContext context = new(3);
            await context.OpenAsync();
            await context.OpenAsync();
            await context.OpenAsync();
            context.Windows[0].Activate();

            await context.OpenAsync();

            context.Windows.Select(window => window.IsVisible).Should().Equal(false, true, true, true);
            context.Windows[0].CloseCleanupCompletion.IsCompletedSuccessfully.Should().BeTrue();
            File.Exists(context.ImagePaths[0]).Should().BeFalse();
            context.ImagePaths.Skip(1).Should().OnlyContain(path => File.Exists(path));
        });
    }

    [Fact]
    public async Task OpenAsync_AfterMiddleWindowIsClosed_PreservesOpeningOrder()
    {
        await DispatchAsync(async () =>
        {
            await using ViewerContext context = new(2);
            await context.OpenAsync();
            await context.OpenAsync();
            context.Windows[1].CaptureRenderedFrame();
            context.Windows[1].Close();
            await context.Windows[1].CloseCleanupCompletion;
            await context.Service.EnforceWindowLimitAsync(CancellationToken.None);
            await context.OpenAsync();

            context.Windows[0].IsVisible.Should().BeTrue();
            context.Windows[2].IsVisible.Should().BeTrue();

            await context.OpenAsync();

            context.Windows.Select(window => window.IsVisible).Should().Equal(false, false, true, true);
        });
    }

    [Fact]
    public async Task OpenAsync_WhenWindowCreationFails_KeepsPreviouslyOpenedWindows()
    {
        await DispatchAsync(async () =>
        {
            await using ViewerContext context = new(1);
            await context.OpenAsync();
            context.FailNextCreation = true;

            Func<Task> open = () => context.OpenAsync();

            await open.Should().ThrowAsync<InvalidOperationException>();
            context.Windows.Should().ContainSingle().Which.IsVisible.Should().BeTrue();
            File.Exists(context.ImagePaths[0]).Should().BeTrue();
        });
    }

    [Fact]
    public async Task OpenAsync_WithConcurrentRequests_KeepsOnlyMostRecentlyOpenedWindows()
    {
        await DispatchAsync(async () =>
        {
            await using ViewerContext context = new(2);

            await Task.WhenAll(context.OpenAsync(), context.OpenAsync(), context.OpenAsync(), context.OpenAsync());

            context.Windows.Select(window => window.IsVisible).Should().Equal(false, false, true, true);
        });
    }

    [Fact]
    public async Task SetLimit_WithExistingWindows_ImmediatelyClosesOldestAndSupportsDisablingLimit()
    {
        await DispatchAsync(async () =>
        {
            await using ViewerContext context = new(ImageViewerWindowLimitSettingDefinition.Unlimited);
            await context.OpenAsync();
            await context.OpenAsync();
            await context.OpenAsync();

            context.Limit.SetLimit(1);
            await context.Service.EnforceWindowLimitAsync(CancellationToken.None);

            context.Windows.Select(window => window.IsVisible).Should().Equal(false, false, true);

            context.Limit.SetLimit(ImageViewerWindowLimitSettingDefinition.Unlimited);
            await context.OpenAsync();
            await context.OpenAsync();

            context.Windows.Skip(2).Should().OnlyContain(window => window.IsVisible);
        });
    }

    private sealed class ViewerContext : IAsyncDisposable
    {
        public ImageViewerService Service { get; }
        public ImageViewerWindowLimitService Limit { get; }
        public List<ImageViewerWindow> Windows { get; } = [];
        public List<string> ImagePaths { get; } = [];
        public bool FailNextCreation { get; set; }

        private static readonly Guid ImageId = Guid.Parse("6f6a08aa-55ee-49c3-9c40-85e7cb0f90c8");
        private static readonly byte[] ImageBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

        private readonly ServiceProvider _provider;
        private readonly TemporaryDirectory _files = new(typeof(ImageViewerWindowLimitTests), nameof(ViewerContext));

        public ViewerContext(int limit)
        {
            ServiceCollection services = new();
            services.AddLogging();
            services.AddPicaViewer();
            Mock<IImageViewerStateService> state = new();
            state.Setup(service => service.LoadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ImageViewerState
                {
                    IsWindowed = true,
                    RememberWindowPlacement = true,
                    AutoHideWindowTitleBar = false,
                    WindowWidth = 640d,
                    WindowHeight = 480d
                });
            services.AddSingleton(state.Object);
            _provider = services.BuildServiceProvider();
            IImageViewerWindowFactory realFactory = _provider.GetRequiredService<IImageViewerWindowFactory>();
            Mock<IImageViewerWindowFactory> factory = new();
            factory.Setup(service => service.CreateAsync(
                    It.IsAny<PicaViewerRequest>(), It.IsAny<IViewerActionDispatcher>(),
                    It.IsAny<IReadOnlyDictionary<Guid, IPicaImageBitmapSource>?>(), It.IsAny<CancellationToken>()))
                .Returns(async (PicaViewerRequest request, IViewerActionDispatcher dispatcher,
                    IReadOnlyDictionary<Guid, IPicaImageBitmapSource>? sources, CancellationToken ct) =>
                {
                    if (FailNextCreation)
                    {
                        throw new InvalidOperationException("Window creation failed.");
                    }

                    ImageViewerWindow window = await realFactory.CreateAsync(request, dispatcher, sources, ct);
                    Windows.Add(window);
                    ImagePaths.Add(request.Items.Single().FilePath);
                    return window;
                });
            SettingsDefinitionCatalog definitions = new(
                new ISettingsDefinition[] { new ImageViewerWindowLimitSettingDefinition() },
                Array.Empty<IUiScaleOptionDefinition>());
            Limit = new ImageViewerWindowLimitService(definitions);
            Limit.SetLimit(limit);
            PicaViewerSessionTestDependencies dependencies = new();
            dependencies.TrustedImageFileService
                .Setup(service => service.GetTrustedImagePath(It.IsAny<string>(), "test-model"))
                .Returns((string path, string _) => path);
            dependencies.PanelAttachments
                .Setup(store => store.ResolveExistingFilePath("test-panel", It.IsAny<PanelAttachmentState>()))
                .Returns((string _, PanelAttachmentState attachment) => Path.Combine(
                    _files.DirectoryPath, attachment.InternalFileName));
            Service = new ImageViewerService(
                factory.Object,
                new PicaViewerSessionFactory(dependencies.CreateDependencies(
                    GenerationImageFormatRegistryTestFactory.Create(), new AvaloniaUiThreadDispatcher())),
                new DataRootAccessCoordinator(),
                Limit,
                NullLogger<ImageViewerService>.Instance);
        }

        public async Task OpenAsync(bool temporary = true)
        {
            string path = Path.Combine(_files.DirectoryPath, Guid.NewGuid().ToString("N") + ".png");
            await File.WriteAllBytesAsync(path, ImageBytes);
            GalleryImageViewerSource source = temporary
                ? new GalleryFileImageViewerSource("test-model", path, DeleteImageWhenClosed: true)
                : new GalleryAttachedImageViewerSource("test-panel", new PanelAttachmentState
                {
                    FileName = "attachment.png",
                    InternalFileName = Path.GetFileName(path)
                });
            GalleryImageViewerItem[] items =
            [
                new GalleryImageViewerItem(ImageId, source)
            ];
            GalleryImageViewerRequest request = new(new GalleryStaticImageViewerItemsSource(items), ImageId, null);

            await Service.OpenAsync(request, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await Service.CloseAllAsync(CancellationToken.None);
            Service.Dispose();
            await _provider.DisposeAsync();
            _files.Dispose();
        }
    }
}
