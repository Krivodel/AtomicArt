using System.Collections.ObjectModel;

using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Controls;
using Avalonia.Media;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Gallery;

public sealed class AnimatedGalleryControlEmptyGenerationTests : AnimatedGalleryControlTestBase
{
    private const int MaxFrameDrainCount = 60;
    private const int FrameStepMilliseconds = 100;
    private const int FirstItemIndex = 0;
    private const int MiddleItemIndex = 1;
    private const int LastItemIndex = 2;
    private const double MovementTolerance = 0.5d;

    private static readonly DateTime CreatedAtUtc = new(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid FirstItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ThirdItemId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid FinalFirstItemId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid FinalSecondItemId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid FinalThirdItemId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task GenerateFrontAsync_WhenEmptyGalleryCreatesOneCard_FirstRemoveCreatesDeleteOverlay()
    {
        await DispatchAsync(async () =>
        {
            TestUiFrameScheduler frameScheduler = new();
            EmptyGenerationScenario scenario = ShowEmptyGenerationScenario(frameScheduler);
            GenerationItemViewModel item = CreateItem(FirstItemId, FirstItemIndex);

            try
            {
                await GenerateFrontAsync(
                    scenario,
                    new List<GenerationItemViewModel> { item },
                    frameScheduler);
                UpdateItemFromResult(item, FinalFirstItemId, FirstItemIndex);

                await RemoveGeneratedItemAsync(scenario, item, frameScheduler);

                GetOverlayCards(scenario.Control)
                    .Should()
                    .ContainSingle(card => ReferenceEquals(card.DataContext, item));
            }
            finally
            {
                await DrainQueuedFramesAsync(frameScheduler);
                scenario.Window.Close();
            }
        });
    }

    [Fact]
    public async Task GenerateFrontAsync_WhenEmptyGalleryCreatesMultipleCards_FirstRemoveCreatesDeleteOverlayAndMovesRemainingCards()
    {
        await DispatchAsync(async () =>
        {
            TestUiFrameScheduler frameScheduler = new();
            EmptyGenerationScenario scenario = ShowEmptyGenerationScenario(frameScheduler);

            try
            {
                List<GenerationItemViewModel> items =
                    await GenerateMultipleItemsAsync(scenario, frameScheduler);

                await RemoveGeneratedItemAsync(scenario, items[FirstItemIndex], frameScheduler);

                GetOverlayCards(scenario.Control)
                    .Should()
                    .ContainSingle(card => ReferenceEquals(card.DataContext, items[FirstItemIndex]));
                AssertCardsStartedMove(
                    scenario.Control,
                    new List<GenerationItemViewModel>
                    {
                        items[MiddleItemIndex],
                        items[LastItemIndex]
                    });
            }
            finally
            {
                await DrainQueuedFramesAsync(frameScheduler);
                scenario.Window.Close();
            }
        });
    }

    [Fact]
    public async Task GenerateFrontAsync_WhenEmptyGalleryCreatesMultipleCards_RemovingNonFirstCardCreatesDeleteOverlayAndMovesRemainingCards()
    {
        await DispatchAsync(async () =>
        {
            TestUiFrameScheduler frameScheduler = new();
            EmptyGenerationScenario scenario = ShowEmptyGenerationScenario(frameScheduler);

            try
            {
                List<GenerationItemViewModel> items =
                    await GenerateMultipleItemsAsync(scenario, frameScheduler);
                GenerationItemViewModel removedItem = items[MiddleItemIndex];

                await RemoveGeneratedItemAsync(scenario, removedItem, frameScheduler);

                GetOverlayCards(scenario.Control)
                    .Should()
                    .ContainSingle(card => ReferenceEquals(card.DataContext, removedItem));
                AssertCardsStartedMove(
                    scenario.Control,
                    new List<GenerationItemViewModel> { items[LastItemIndex] });
                AssertCardsNotStartedMove(
                    scenario.Control,
                    new List<GenerationItemViewModel> { items[FirstItemIndex] });
            }
            finally
            {
                await DrainQueuedFramesAsync(frameScheduler);
                scenario.Window.Close();
            }
        });
    }

    private static EmptyGenerationScenario ShowEmptyGenerationScenario(TestUiFrameScheduler frameScheduler)
    {
        IAnimatedGallerySceneFactory sceneFactory = CreateSceneFactory(frameScheduler);
        AnimatedGalleryOperations operations = new(
            sceneFactory,
            NullLogger<AnimatedGalleryOperations>.Instance);
        ObservableCollection<GenerationItemViewModel> items = [];
        AnimatedGalleryControl control = new(sceneFactory)
        {
            Items = items,
            Operations = operations
        };
        Window window = Show(control, 560d, 640d);

        DrainQueuedFrames(frameScheduler);

        return new EmptyGenerationScenario(control, window, operations, items);
    }

    private static async Task GenerateFrontAsync(
        EmptyGenerationScenario scenario,
        IReadOnlyList<GenerationItemViewModel> items,
        TestUiFrameScheduler frameScheduler)
    {
        foreach (GenerationItemViewModel item in items)
        {
            scenario.Items.Add(item);
        }

        Task generationTask = scenario.Operations.GenerateFrontAsync(items.Cast<object>().ToList(), CancellationToken.None);
        await RunQueuedFramesUntilCompletedAsync(frameScheduler, generationTask);
        await generationTask;

        AssertGeneratedCardsReady(scenario.Control, items);
    }

    private static async Task<List<GenerationItemViewModel>> GenerateMultipleItemsAsync(
        EmptyGenerationScenario scenario,
        TestUiFrameScheduler frameScheduler)
    {
        List<GenerationItemViewModel> items =
        [
            CreateItem(FirstItemId, FirstItemIndex),
            CreateItem(SecondItemId, MiddleItemIndex),
            CreateItem(ThirdItemId, LastItemIndex)
        ];
        Guid[] finalIds =
        [
            FinalFirstItemId,
            FinalSecondItemId,
            FinalThirdItemId
        ];

        await GenerateFrontAsync(scenario, items, frameScheduler);
        UpdateItemsFromResults(items, finalIds);

        return items;
    }

    private static async Task RemoveGeneratedItemAsync(
        EmptyGenerationScenario scenario,
        GenerationItemViewModel item,
        TestUiFrameScheduler frameScheduler)
    {
        scenario.Items.Remove(item).Should().BeTrue();

        Task removeTask = scenario.Operations.RemoveAsync(item.Id, CancellationToken.None);
        await RunQueuedFramesUntilCompletedAsync(frameScheduler, removeTask);
        await removeTask;
    }

    private static void AssertGeneratedCardsReady(
        AnimatedGalleryControl control,
        IReadOnlyList<GenerationItemViewModel> items)
    {
        foreach (GenerationItemViewModel item in items)
        {
            GenerationCardControl card = GetVisibleCard(control, item);

            card.Width.Should().Be(GalleryLayoutService.CardWidth);
            card.Height.Should().Be(GalleryLayoutService.CardHeight);
            Canvas.GetLeft(card).Should().BeGreaterThanOrEqualTo(0d);
            Canvas.GetTop(card).Should().BeGreaterThanOrEqualTo(GalleryLayoutService.CardTopPadding);
        }
    }

    private static GenerationCardControl GetVisibleCard(
        AnimatedGalleryControl control,
        GenerationItemViewModel item)
    {
        return GetVisibleCards(control)
            .Single(card => ReferenceEquals(card.DataContext, item));
    }

    private static List<GenerationCardControl> GetVisibleCards(AnimatedGalleryControl control)
    {
        return GetGalleryPanel(control)
            .Children
            .OfType<GenerationCardControl>()
            .ToList();
    }

    private static List<GenerationCardControl> GetOverlayCards(AnimatedGalleryControl control)
    {
        return GetOverlayCanvas(control)
            .Children
            .OfType<GenerationCardControl>()
            .ToList();
    }

    private static bool HasStartedMove(GenerationCardControl card)
    {
        TranslateTransform transform = GetTranslateTransform(card);

        return (Math.Abs(transform.X) > MovementTolerance)
               || (Math.Abs(transform.Y) > MovementTolerance);
    }

    private static void AssertCardsStartedMove(
        AnimatedGalleryControl control,
        IReadOnlyList<GenerationItemViewModel> items)
    {
        foreach (GenerationItemViewModel item in items)
        {
            GenerationCardControl card = GetVisibleCard(control, item);

            HasStartedMove(card).Should().BeTrue();
        }
    }

    private static void AssertCardsNotStartedMove(
        AnimatedGalleryControl control,
        IReadOnlyList<GenerationItemViewModel> items)
    {
        foreach (GenerationItemViewModel item in items)
        {
            GenerationCardControl card = GetVisibleCard(control, item);

            HasStartedMove(card).Should().BeFalse();
        }
    }

    private static async Task RunQueuedFramesUntilCompletedAsync(
        TestUiFrameScheduler frameScheduler,
        Task task)
    {
        int frameIndex = 0;
        while (!task.IsCompleted && (frameIndex < MaxFrameDrainCount))
        {
            if (frameScheduler.HasQueuedFrame)
            {
                await frameScheduler.RunNextFrameAsync(TimeSpan.FromMilliseconds(frameIndex * FrameStepMilliseconds));
            }
            else
            {
                await TestUiFrameScheduler.RunQueuedContinuationsAsync();
            }

            frameIndex++;
        }

        task.IsCompleted.Should().BeTrue();
    }

    private static void DrainQueuedFrames(TestUiFrameScheduler frameScheduler)
    {
        int frameIndex = 0;
        while (frameScheduler.HasQueuedFrame && (frameIndex < MaxFrameDrainCount))
        {
            frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(frameIndex * FrameStepMilliseconds));
            frameIndex++;
        }
    }

    private static async Task DrainQueuedFramesAsync(TestUiFrameScheduler frameScheduler)
    {
        int frameIndex = 0;
        while (frameScheduler.HasQueuedFrame && (frameIndex < MaxFrameDrainCount))
        {
            await frameScheduler.RunNextFrameAsync(TimeSpan.FromMilliseconds(frameIndex * FrameStepMilliseconds));
            frameIndex++;
        }
    }

    private static GenerationItemViewModel CreateItem(Guid id, int index)
    {
        GenerationItemDto item = GenerationItemDtoTestFactory.Create(
            id: id,
            modelId: "test-model",
            modelDisplayName: "Test Model",
            prompt: $"Prompt {index}",
            aspectRatio: "1:1",
            createdAtUtc: CreatedAtUtc);

        return new GenerationItemViewModel(
            item,
            index,
            null,
            GenerationItemStatusDescriptorRegistryTestFactory.Create());
    }

    private static void UpdateItemsFromResults(
        IReadOnlyList<GenerationItemViewModel> items,
        IReadOnlyList<Guid> finalIds)
    {
        finalIds.Should().HaveCount(items.Count);

        for (int index = 0; index < items.Count; index++)
        {
            UpdateItemFromResult(items[index], finalIds[index], index);
        }
    }

    private static void UpdateItemFromResult(
        GenerationItemViewModel item,
        Guid finalId,
        int index)
    {
        GenerationItemDto result = GenerationItemDtoTestFactory.Create(
            id: finalId,
            modelId: "test-model",
            modelDisplayName: "Test Model",
            prompt: $"Prompt {index}",
            aspectRatio: "1:1",
            createdAtUtc: CreatedAtUtc);

        item.UpdateFromResult(result, null, null);
    }

    private sealed record EmptyGenerationScenario(
        AnimatedGalleryControl Control,
        Window Window,
        AnimatedGalleryOperations Operations,
        ObservableCollection<GenerationItemViewModel> Items);
}
