using System.Globalization;

using Microsoft.Extensions.DependencyInjection;

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
            GalleryViewScenario scenario = CreateGalleryViewScenario(serviceProvider);
            Window window = Show(scenario.View);

            try
            {
                AssertGalleryViewOperations(scenario.View);
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
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            GalleryViewScenario scenario = CreateGalleryViewScenario(serviceProvider);
            Window window = Show(scenario.View);

            try
            {
                await scenario.ViewModel.RestoreStateAsync(
                    new GalleryItemState[] { GalleryItemStateTestFactory.CreateGenerated() },
                    CancellationToken.None);
                window.CaptureRenderedFrame();

                AssertSingleVisibleCard(GetGalleryControl(scenario.View));
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
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            GalleryViewScenario scenario = CreateGalleryViewScenario(serviceProvider);

            await scenario.ViewModel.RestoreStateAsync(
                new GalleryItemState[] { GalleryItemStateTestFactory.CreateGenerated() },
                CancellationToken.None);

            Window window = Show(scenario.View);

            try
            {
                AssertSingleVisibleCard(GetGalleryControl(scenario.View));
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
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            MainWindowScenario scenario = CreateMainWindowScenario(serviceProvider);

            await scenario.ViewModel.RestoreGalleryAsync(
                new GalleryItemState[] { GalleryItemStateTestFactory.CreateGenerated() },
                CancellationToken.None);

            ShowAndAssertSingleVisibleCard(scenario.Window);
        });
    }

    [Fact]
    public async Task MainWindowRestoreAppStateCommand_BeforeShow_RendersVisibleGalleryCardAfterShow()
    {
        await DispatchAsync(async () =>
        {
            GalleryItemState savedItem = GalleryItemStateTestFactory.CreateGenerated();
            await using ServiceProvider serviceProvider = CreateServiceProvider(
                new FixedGalleryAppStateBootstrapper(savedItem));
            MainWindowScenario scenario = CreateMainWindowScenario(serviceProvider);

            await scenario.ViewModel.RestoreAppStateCommand.ExecuteAsync(null);

            ShowAndAssertSingleVisibleCard(scenario.Window);
        });
    }

    [Fact]
    public async Task MainWindowRestoreAppStateCommand_FireAndForgetBeforeShow_RendersVisibleGalleryCardAfterShow()
    {
        await DispatchAsync(async () =>
        {
            GalleryItemState savedItem = GalleryItemStateTestFactory.CreateGenerated();
            await using ServiceProvider serviceProvider = CreateServiceProvider(
                new FixedGalleryAppStateBootstrapper(savedItem));
            MainWindowScenario scenario = CreateMainWindowScenario(serviceProvider);

            Task restoreTask = scenario.ViewModel.RestoreAppStateCommand.ExecuteAsync(null);
            scenario.Window.Show();
            scenario.Window.CaptureRenderedFrame();

            try
            {
                await restoreTask;
                scenario.Window.CaptureRenderedFrame();

                AssertSingleVisibleCard(GetGalleryControl(scenario.Window));
            }
            finally
            {
                scenario.Window.Close();
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
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            MainWindowScenario scenario = CreateMainWindowScenario(serviceProvider);
            IReadOnlyList<GalleryItemState> items = CreateSavedGalleryItems(itemCount);

            await scenario.ViewModel.RestoreGalleryAsync(items, CancellationToken.None);

            scenario.Window.Show();
            scenario.Window.CaptureRenderedFrame();

            try
            {
                scenario.ViewModel.Gallery.Items.Should().HaveCount(itemCount);
                AnimatedGalleryControl control = scenario.Window
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
                scenario.Window.Close();
            }
        });
    }

    private static GalleryViewScenario CreateGalleryViewScenario(
        IServiceProvider serviceProvider)
    {
        GalleryViewModel viewModel = serviceProvider.GetRequiredService<GalleryViewModel>();
        GalleryView view = serviceProvider.GetRequiredService<GalleryView>();
        view.DataContext = viewModel;

        return new GalleryViewScenario(view, viewModel);
    }

    private static MainWindowScenario CreateMainWindowScenario(
        IServiceProvider serviceProvider)
    {
        RegisterGalleryViewTemplate(serviceProvider);
        MainWindow window = serviceProvider.GetRequiredService<MainWindow>();
        MainWindowViewModel viewModel = window.DataContext
            .Should()
            .BeOfType<MainWindowViewModel>()
            .Subject;

        return new MainWindowScenario(window, viewModel);
    }

    private static ServiceProvider CreateServiceProvider(
        IAppStateBootstrapper? appStateBootstrapper = null)
    {
        ServiceCollection services = new();
        services.AddSingleton(TestApiConfiguration.Create());
        services.AddDesktopServices();

        if (appStateBootstrapper is not null)
        {
            services.AddSingleton(appStateBootstrapper);
        }

        return services.BuildServiceProvider();
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

    private static void ShowAndAssertSingleVisibleCard(Window window)
    {
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

    private static void RegisterGalleryViewTemplate(IServiceProvider serviceProvider)
    {
        Avalonia.Application.Current?.DataTemplates.Add(
            new ViewModelViewTemplate(
                new ViewTemplateRegistration[]
                {
                    new ViewTemplateRegistration(
                        typeof(GalleryViewModel),
                        serviceProvider.GetRequiredService<GalleryView>)
                }));
    }

    private sealed record MainWindowScenario(
        MainWindow Window,
        MainWindowViewModel ViewModel);

    private sealed record GalleryViewScenario(
        GalleryView View,
        GalleryViewModel ViewModel);

    private sealed class FixedGalleryAppStateBootstrapper : IAppStateBootstrapper
    {
        private readonly GalleryItemState _savedItem;

        public FixedGalleryAppStateBootstrapper(GalleryItemState savedItem)
        {
            _savedItem = savedItem;
        }

        public Task RestoreAsync(IAppStateRestoreTarget target, CancellationToken ct)
        {
            GalleryItemState[] savedItems = [_savedItem];

            return target.RestoreGalleryAsync(savedItems, ct);
        }

        public Task FlushAsync(IAppStateFlushTarget target, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
