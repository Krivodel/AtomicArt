using Avalonia;
using Avalonia.Controls;

using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Services.Windowing;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Windowing;

public sealed class GenerationPanelLayoutTrackerTests : DesktopControlTestBase
{
    private const double DefaultWidth = 640d;
    private const double MinimumHeight = 200d;

    [Fact]
    public async Task AttachAsync_WithSavedDimensions_RestoresWithoutSchedulingWrite()
    {
        await DispatchAsync(async () =>
        {
            GenerationPanelLayoutState saved = new() { Width = 1200d, Height = 450d };
            RecordingStateWriteScheduler scheduler = new();
            using GenerationPanelLayoutTracker tracker = CreateTracker(new StubAppStateStore(saved), scheduler);
            Border panel = new() { MaxWidth = DefaultWidth };
            RowDefinition row = new() { MinHeight = MinimumHeight };

            await tracker.AttachAsync(panel, row);

            panel.MaxWidth.Should().Be(saved.Width);
            row.Height.Should().Be(new GridLength(saved.Height.GetValueOrDefault()));
            row.MinHeight.Should().Be(MinimumHeight);
            scheduler.CallCount.Should().Be(0);
        });
    }

    [Fact]
    public async Task AttachAsync_WithoutSavedDimensions_PreservesAutomaticHeight()
    {
        await DispatchAsync(async () =>
        {
            RecordingStateWriteScheduler scheduler = new();
            using GenerationPanelLayoutTracker tracker = CreateTracker(
                new StubAppStateStore(new GenerationPanelLayoutState()), scheduler);
            Border panel = new() { MaxWidth = DefaultWidth };
            RowDefinition row = new() { Height = GridLength.Auto, MinHeight = MinimumHeight };

            await tracker.AttachAsync(panel, row);

            panel.MaxWidth.Should().Be(DefaultWidth);
            row.Height.Should().Be(GridLength.Auto);
            scheduler.CallCount.Should().Be(0);
        });
    }

    [Fact]
    public async Task AttachAsync_WithSmallSavedDimensions_RespectsCurrentMinimums()
    {
        await DispatchAsync(async () =>
        {
            RecordingStateWriteScheduler scheduler = new();
            using GenerationPanelLayoutTracker tracker = CreateTracker(
                new StubAppStateStore(new GenerationPanelLayoutState { Width = 1d, Height = 1d }), scheduler);
            Border panel = new() { MaxWidth = DefaultWidth };
            RowDefinition row = new() { MinHeight = MinimumHeight };

            await tracker.AttachAsync(panel, row);

            panel.MaxWidth.Should().Be(DefaultWidth);
            row.Height.Should().Be(new GridLength(MinimumHeight));
        });
    }

    [Fact]
    public async Task Dimensions_WhenChanged_QueuesIndependentSnapshotsForBothAxes()
    {
        await DispatchAsync(async () =>
        {
            RecordingStateWriteScheduler scheduler = new();
            using GenerationPanelLayoutTracker tracker = CreateTracker(
                new StubAppStateStore(new GenerationPanelLayoutState()), scheduler);
            Border panel = new() { MaxWidth = DefaultWidth };
            RowDefinition row = new() { Height = GridLength.Auto, MinHeight = MinimumHeight };
            await tracker.AttachAsync(panel, row);

            panel.MaxWidth = 1000d;

            GenerationPanelLayoutState widthSnapshot = scheduler.SavedState.Should()
                .BeOfType<GenerationPanelLayoutState>().Subject;
            widthSnapshot.Width.Should().Be(1000d);
            widthSnapshot.Height.Should().BeNull();

            row.Height = new GridLength(500d);

            GenerationPanelLayoutState sizeSnapshot = scheduler.SavedState.Should()
                .BeOfType<GenerationPanelLayoutState>().Subject;
            sizeSnapshot.Width.Should().Be(1000d);
            sizeSnapshot.Height.Should().Be(500d);
            widthSnapshot.Height.Should().BeNull();
            scheduler.SavedSection.Should().BeOfType<GenerationPanelLayoutStateSection>();
            scheduler.SavedMode.Should().Be(StateWriteMode.Deferred);
        });
    }

    [Theory]
    [InlineData("width")]
    [InlineData("height")]
    [InlineData("width-returned")]
    public async Task AttachAsync_WhenResizedDuringLoad_PreservesUserChangeAndRestoresOtherAxis(string changedAxis)
    {
        await DispatchAsync(async () =>
        {
            TaskCompletionSource<GenerationPanelLayoutState> load = new();
            Mock<IAppStateStore> store = new();
            store.Setup(service => service.LoadAsync<GenerationPanelLayoutState>(
                    It.IsAny<IStateSection>(), It.IsAny<CancellationToken>()))
                .Returns(load.Task);
            RecordingStateWriteScheduler scheduler = new();
            using GenerationPanelLayoutTracker tracker = CreateTracker(store.Object, scheduler);
            Border panel = new() { MaxWidth = DefaultWidth };
            RowDefinition row = new() { Height = GridLength.Auto, MinHeight = MinimumHeight };
            Task attaching = tracker.AttachAsync(panel, row);

            if (changedAxis.StartsWith("width", StringComparison.Ordinal))
            {
                panel.MaxWidth = 900d;

                if (changedAxis == "width-returned")
                {
                    panel.MaxWidth = DefaultWidth;
                }
            }
            else
            {
                row.Height = new GridLength(350d);
            }

            load.SetResult(new GenerationPanelLayoutState { Width = 1200d, Height = 500d });
            await attaching;

            double expectedWidth = changedAxis switch
            {
                "width" => 900d,
                "width-returned" => DefaultWidth,
                _ => 1200d
            };
            panel.MaxWidth.Should().Be(expectedWidth);
            row.Height.Value.Should().Be(changedAxis == "height" ? 350d : 500d);
            GenerationPanelLayoutState state = scheduler.SavedState.Should()
                .BeOfType<GenerationPanelLayoutState>().Subject;
            state.Width.Should().Be(panel.MaxWidth);
            state.Height.Should().Be(row.Height.Value);
        });
    }

    [Fact]
    public async Task AttachAsync_WhenLoadFails_PreservesDefaultsAndContinuesSaving()
    {
        await DispatchAsync(async () =>
        {
            Mock<IAppStateStore> store = new();
            store.Setup(service => service.LoadAsync<GenerationPanelLayoutState>(
                    It.IsAny<IStateSection>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("State is unavailable."));
            RecordingStateWriteScheduler scheduler = new();
            using GenerationPanelLayoutTracker tracker = CreateTracker(store.Object, scheduler);
            Border panel = new() { MaxWidth = DefaultWidth };
            RowDefinition row = new() { Height = GridLength.Auto };

            await tracker.AttachAsync(panel, row);

            panel.MaxWidth.Should().Be(DefaultWidth);
            row.Height.Should().Be(GridLength.Auto);
            scheduler.CallCount.Should().Be(0);

            panel.MaxWidth = 1000d;

            scheduler.SavedState.Should().BeOfType<GenerationPanelLayoutState>()
                .Which.Width.Should().Be(1000d);
        });
    }

    [Fact]
    public async Task Dispose_AfterAttach_UnsubscribesFromDimensionChanges()
    {
        await DispatchAsync(async () =>
        {
            RecordingStateWriteScheduler scheduler = new();
            using GenerationPanelLayoutTracker tracker = CreateTracker(
                new StubAppStateStore(new GenerationPanelLayoutState()), scheduler);
            Border panel = new() { MaxWidth = DefaultWidth };
            RowDefinition row = new() { Height = GridLength.Auto };
            await tracker.AttachAsync(panel, row);

            tracker.Dispose();
            panel.MaxWidth = 1000d;
            row.Height = new GridLength(500d);

            scheduler.CallCount.Should().Be(0);
        });
    }

    [Fact]
    public async Task Dispose_DuringLoad_CancelsAndPreventsLateRestoreOrWrites()
    {
        await DispatchAsync(async () =>
        {
            TaskCompletionSource<GenerationPanelLayoutState> load = new();
            CancellationToken loadToken = default;
            Mock<IAppStateStore> store = new();
            store.Setup(service => service.LoadAsync<GenerationPanelLayoutState>(
                    It.IsAny<IStateSection>(), It.IsAny<CancellationToken>()))
                .Callback<IStateSection, CancellationToken>((section, ct) => loadToken = ct)
                .Returns(load.Task);
            RecordingStateWriteScheduler scheduler = new();
            using GenerationPanelLayoutTracker tracker = CreateTracker(store.Object, scheduler);
            Border panel = new() { MaxWidth = DefaultWidth };
            RowDefinition row = new() { Height = GridLength.Auto };
            Task attaching = tracker.AttachAsync(panel, row);

            tracker.Dispose();
            load.SetResult(new GenerationPanelLayoutState { Width = 1200d, Height = 500d });
            await attaching;
            panel.MaxWidth = 900d;
            row.Height = new GridLength(350d);

            loadToken.IsCancellationRequested.Should().BeTrue();
            panel.MaxWidth.Should().Be(900d);
            row.Height.Value.Should().Be(350d);
            scheduler.CallCount.Should().Be(0);
        });
    }

    private static GenerationPanelLayoutTracker CreateTracker(
        IAppStateStore store,
        IStateWriteScheduler scheduler)
    {
        return new GenerationPanelLayoutTracker(
            store,
            scheduler,
            new GenerationPanelLayoutStateSection(),
            NullLogger<GenerationPanelLayoutTracker>.Instance);
    }
}
