using Microsoft.Extensions.DependencyInjection;

using System.Globalization;

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Desktop.Views.Shell;

namespace AtomicArt.Desktop.Tests.Views.Gallery;

public sealed class GalleryViewTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void GalleryViewBinding_WithViewModelFromContainer_PassesFacadeAndRegistersCoordinator()
    {
        Dispatch(() =>
        {
            ServiceCollection services = new();
            services.AddDesktopServices();
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            GalleryViewModel viewModel = serviceProvider.GetRequiredService<GalleryViewModel>();
            GalleryView view = CreateGalleryView(serviceProvider, viewModel);
            Window window = Show(view);

            try
            {
                AssertGalleryViewOperations(view);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task GalleryViewRestoreStateAsync_WithSavedItem_RendersVisibleCard()
    {
        await DispatchAsync(async () =>
        {
            ServiceCollection services = new();
            services.AddSingleton(TestApiConfiguration.Create());
            services.AddDesktopServices();
            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            GalleryViewModel viewModel = serviceProvider.GetRequiredService<GalleryViewModel>();
            GalleryView view = CreateGalleryView(serviceProvider, viewModel);
            Window window = Show(view);

            try
            {
                await viewModel.RestoreStateAsync(
                    [GalleryItemStateTestFactory.CreateGenerated()],
                    CancellationToken.None);
                window.CaptureRenderedFrame();

                AssertSingleVisibleCard(GetGalleryControl(view));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task GalleryViewRestoreStateAsync_BeforeAttach_RendersVisibleCardAfterAttach()
    {
        await DispatchAsync(async () =>
        {
            ServiceCollection services = new();
            services.AddSingleton(TestApiConfiguration.Create());
            services.AddDesktopServices();
            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            GalleryViewModel viewModel = serviceProvider.GetRequiredService<GalleryViewModel>();
            GalleryView view = CreateGalleryView(serviceProvider, viewModel);

            await viewModel.RestoreStateAsync(
                [GalleryItemStateTestFactory.CreateGenerated()],
                CancellationToken.None);

            Window window = Show(view);

            try
            {
                AssertSingleVisibleCard(GetGalleryControl(view));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task MainWindowRestoreGalleryAsync_BeforeShow_RendersVisibleGalleryCardAfterShow()
    {
        await DispatchAsync(async () =>
        {
            ServiceCollection services = new();
            services.AddSingleton(TestApiConfiguration.Create());
            services.AddDesktopServices();
            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            RegisterGalleryViewTemplate(serviceProvider);
            MainWindow window = serviceProvider.GetRequiredService<MainWindow>();
            MainWindowViewModel viewModel = window.DataContext
                .Should()
                .BeOfType<MainWindowViewModel>()
                .Subject;

            await viewModel.RestoreGalleryAsync(
                [GalleryItemStateTestFactory.CreateGenerated()],
                CancellationToken.None);

            window.Show();
            window.CaptureRenderedFrame();

            try
            {
                AssertSingleVisibleCard(GetGalleryControl(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task MainWindowRestoreAppStateCommand_BeforeShow_RendersVisibleGalleryCardAfterShow()
    {
        await DispatchAsync(async () =>
        {
            GalleryItemState savedItem = GalleryItemStateTestFactory.CreateGenerated();
            ServiceCollection services = new();
            services.AddSingleton(TestApiConfiguration.Create());
            services.AddDesktopServices();
            services.AddSingleton<IAppStateBootstrapper>(new FixedGalleryAppStateBootstrapper(savedItem));
            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            RegisterGalleryViewTemplate(serviceProvider);
            MainWindow window = serviceProvider.GetRequiredService<MainWindow>();
            MainWindowViewModel viewModel = window.DataContext
                .Should()
                .BeOfType<MainWindowViewModel>()
                .Subject;

            await viewModel.RestoreAppStateCommand.ExecuteAsync(null);

            window.Show();
            window.CaptureRenderedFrame();

            try
            {
                AssertSingleVisibleCard(GetGalleryControl(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task MainWindowRestoreAppStateCommand_FireAndForgetBeforeShow_RendersVisibleGalleryCardAfterShow()
    {
        await DispatchAsync(async () =>
        {
            GalleryItemState savedItem = GalleryItemStateTestFactory.CreateGenerated();
            ServiceCollection services = new();
            services.AddSingleton(TestApiConfiguration.Create());
            services.AddDesktopServices();
            services.AddSingleton<IAppStateBootstrapper>(new FixedGalleryAppStateBootstrapper(savedItem));
            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            RegisterGalleryViewTemplate(serviceProvider);
            MainWindow window = serviceProvider.GetRequiredService<MainWindow>();
            MainWindowViewModel viewModel = window.DataContext
                .Should()
                .BeOfType<MainWindowViewModel>()
                .Subject;

            Task restoreTask = viewModel.RestoreAppStateCommand.ExecuteAsync(null);
            window.Show();
            window.CaptureRenderedFrame();

            try
            {
                await restoreTask;
                window.CaptureRenderedFrame();

                AssertSingleVisibleCard(GetGalleryControl(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(30)]
    [InlineData(200)]
    public async Task MainWindowRestoreGalleryAsync_WithManySavedItems_RendersVisibleCards(int itemCount)
    {
        await DispatchAsync(async () =>
        {
            ServiceCollection services = new();
            services.AddSingleton(TestApiConfiguration.Create());
            services.AddDesktopServices();
            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            RegisterGalleryViewTemplate(serviceProvider);
            MainWindow window = serviceProvider.GetRequiredService<MainWindow>();
            MainWindowViewModel viewModel = window.DataContext
                .Should()
                .BeOfType<MainWindowViewModel>()
                .Subject;
            IReadOnlyList<GalleryItemState> items = CreateSavedGalleryItems(itemCount);

            await viewModel.RestoreGalleryAsync(items, CancellationToken.None);

            window.Show();
            window.CaptureRenderedFrame();

            try
            {
                viewModel.Gallery.Items.Should().HaveCount(itemCount);
                AnimatedGalleryControl control = window
                    .GetVisualDescendants()
                    .OfType<AnimatedGalleryControl>()
                    .Single();
                Canvas galleryPanel = GetGalleryPanel(control);

                galleryPanel.Children.OfType<Control>().Should().NotBeEmpty();
                galleryPanel.Children.OfType<Control>().Should().OnlyContain(card =>
                    card.IsVisible
                    && card.Opacity > 0d
                    && card.Width > 0d
                    && card.Height > 0d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static GalleryView CreateGalleryView(
        IServiceProvider serviceProvider,
        GalleryViewModel viewModel)
    {
        GalleryView view = serviceProvider.GetRequiredService<GalleryView>();
        view.DataContext = viewModel;

        return view;
    }

    private static void AssertGalleryViewOperations(GalleryView view)
    {
        AnimatedGalleryControl control = GetGalleryControl(view);

        AnimatedGalleryOperations operations = control
            .Operations
            .Should()
            .BeOfType<AnimatedGalleryOperations>()
            .Subject;
        operations.ActiveOperations.Should().BeOfType<GalleryOperationCoordinator>();
    }

    private static void AssertSingleVisibleCard(AnimatedGalleryControl control)
    {
        Canvas galleryPanel = GetGalleryPanel(control);
        Control card = galleryPanel.Children.OfType<Control>().Single();

        card.IsVisible.Should().BeTrue();
        card.Opacity.Should().Be(1d);
        card.Width.Should().BeGreaterThan(0d);
        card.Height.Should().BeGreaterThan(0d);
    }

    private static AnimatedGalleryControl GetGalleryControl(GalleryView view)
    {
        return view
            .GetVisualDescendants()
            .OfType<AnimatedGalleryControl>()
            .Single();
    }

    private static AnimatedGalleryControl GetGalleryControl(Window window)
    {
        return window
            .GetVisualDescendants()
            .OfType<AnimatedGalleryControl>()
            .Single();
    }

    private static IReadOnlyList<GalleryItemState> CreateSavedGalleryItems(int count)
    {
        List<GalleryItemState> items = [];

        for (int i = 0; i < count; i++)
        {
            string prompt = string.Concat("Saved prompt ", i.ToString(CultureInfo.InvariantCulture));
            items.Add(GalleryItemStateTestFactory.CreateGenerated(prompt, i));
        }

        return items;
    }

    private static void RegisterGalleryViewTemplate(IServiceProvider serviceProvider)
    {
        Avalonia.Application.Current?.DataTemplates.Add(
            new ViewModelViewTemplate(
            [
                new ViewTemplateRegistration(
                    typeof(GalleryViewModel),
                    serviceProvider.GetRequiredService<GalleryView>)
            ]));
    }

    private sealed class FixedGalleryAppStateBootstrapper : IAppStateBootstrapper
    {
        private readonly GalleryItemState _savedItem;

        public FixedGalleryAppStateBootstrapper(GalleryItemState savedItem)
        {
            _savedItem = savedItem;
        }

        public Task RestoreAsync(IAppStateRestoreTarget target, CancellationToken ct)
        {
            return target.RestoreGalleryAsync([_savedItem], ct);
        }

        public Task FlushAsync(IAppStateFlushTarget target, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
