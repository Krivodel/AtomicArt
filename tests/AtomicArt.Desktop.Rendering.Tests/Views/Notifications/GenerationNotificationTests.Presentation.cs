using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.Tests.Services.Notifications;
using AtomicArt.Desktop.ViewModels.Notifications;
using AtomicArt.Desktop.Views.Notifications;

namespace AtomicArt.Desktop.Rendering.Tests.Views.Notifications;

public sealed partial class GenerationNotificationTests
{
    [Fact]
    public async Task ShowAsync_WithTextReply_AnimatesWithoutAnImageOrEmptySpace()
    {
        await DispatchAsync(typeof(GenerationNotificationTests), async () =>
        {
            await using NotificationPresentationTestContext context = new();
            Task showing = context.StartAsync("Короткий промпт");
            await context.WaitForVisibleAsync(1);
            await context.Clock.WaitForTimerCountAsync(1, context.Token);
            GenerationNotificationWindow window = context.Windows.Single();
            Border card = GetCard(window);
            AnimatedTransformState motion = AnimatedTransformState.GetOrCreate(card);

            card.Opacity.Should().Be(0);
            await context.StepAsync(0);
            await context.StepAsync(170);

            card.Opacity.Should().BeGreaterThan(0).And.BeLessThan(1);
            motion.Translate.Y.Should().BeLessThan(28).And.BeGreaterThan(-2);
            motion.Rotate.Angle.Should().BeGreaterThan(-1.8);
            SaveFrame(window, "notification-opening.png");
            await context.CompleteAnimationsAsync();
            await window.OpeningAnimation;
            motion.Translate.Y.Should().Be(0);
            motion.Rotate.Angle.Should().Be(0);
            motion.Scale.ScaleX.Should().Be(1);
            card.Opacity.Should().Be(1);
            TextBlock prompt = card.GetVisualDescendants().OfType<TextBlock>()
                .Single(text => text.Text == "Короткий промпт");
            Point promptTop = prompt.TranslatePoint(default, card)
                ?? throw new InvalidOperationException("The prompt is not attached to the notification.");
            (card.Bounds.Height - promptTop.Y - prompt.Bounds.Height)
                .Should().BeApproximately(card.Padding.Bottom, 1);
            card.GetVisualDescendants().OfType<Image>().Should().ContainSingle();
            SaveFrame(window, "notification-text.png");
            context.Clock.Timers.Single().DueTime.Should().Be(TimeSpan.FromSeconds(5));

            context.Clock.Timers.Single().Fire();
            await context.WaitForClosingAsync(window);
            await context.StepAsync(0);
            await context.StepAsync(100);
            window.IsVisible.Should().BeTrue();
            card.Opacity.Should().BeGreaterThan(0).And.BeLessThan(1);
            SaveFrame(window, "notification-closing.png");
            await context.CompleteAnimationsAsync();
            await showing.WaitAsync(context.Token);
            window.IsVisible.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShowAsync_WithHiddenOrMinimizedMainWindow_DoesNotActivateIt(bool hideToTray)
    {
        await DispatchAsync(typeof(GenerationNotificationTests), async () =>
        {
            await using NotificationPresentationTestContext context = new();
            Window main = new();
            main.Show();

            try
            {
                if (hideToTray)
                {
                    main.Hide();
                }
                else
                {
                    main.WindowState = WindowState.Minimized;
                }

                _ = context.StartAsync();
                await context.WaitForVisibleAsync(1);
                await context.CompleteAnimationsAsync();
                GenerationNotificationWindow notification = context.Windows.Single();
                notification.ShowActivated.Should().BeFalse();
                notification.ShowInTaskbar.Should().BeFalse();
                notification.Topmost.Should().BeTrue();
                context.WindowState.VerifyNoOtherCalls();

                if (hideToTray)
                {
                    main.IsVisible.Should().BeFalse();
                }
                else
                {
                    main.WindowState.Should().Be(WindowState.Minimized);
                }
            }
            finally
            {
                main.Close();
            }
        });
    }

    [Fact]
    public async Task ShowAsync_WhenAtomicArtIsFocused_PlaysSoundWithoutCreatingAWindow()
    {
        await DispatchAsync(typeof(GenerationNotificationTests), async () =>
        {
            await using NotificationPresentationTestContext context = new();
            context.Focus.SetupGet(focus => focus.IsFocused).Returns(true);

            await context.StartAsync();

            context.Windows.Should().BeEmpty();
            context.Clock.Timers.Should().BeEmpty();
            context.Sound.Verify(sound => sound.Play(It.IsAny<CancellationToken>()), Times.Once);
        });
    }

    [Fact]
    public async Task ShowAsync_WhenFocusChangesWhileWaiting_DoesNotShowTheQueuedNotification()
    {
        await DispatchAsync(typeof(GenerationNotificationTests), async () =>
        {
            await using NotificationPresentationTestContext context = new();
            Task first = context.StartAsync("Первый");
            _ = context.StartAsync("Второй");
            _ = context.StartAsync("Третий");
            Task queued = context.StartAsync("Очередь");
            await context.WaitForVisibleAsync(3);
            await context.CompleteAnimationsAsync();
            context.Focus.SetupGet(focus => focus.IsFocused).Returns(true);

            context.Windows[0].RequestClose();
            await context.CompleteAnimationsAsync();
            await Task.WhenAll(first, queued).WaitAsync(context.Token);

            context.Windows.Should().HaveCount(3);
            context.Sound.Verify(sound => sound.Play(It.IsAny<CancellationToken>()), Times.Exactly(4));
        });
    }

    [Fact]
    public async Task CloseCommand_DuringAppearance_ContinuesFromTheCurrentFrame()
    {
        await DispatchAsync(typeof(GenerationNotificationTests), async () =>
        {
            await using NotificationPresentationTestContext context = new();
            Task showing = context.StartAsync();
            await context.WaitForVisibleAsync(1);
            GenerationNotificationWindow window = context.Windows.Single();
            Border card = GetCard(window);
            AnimatedTransformState motion = AnimatedTransformState.GetOrCreate(card);
            await context.StepAsync(0);
            await context.StepAsync(70);
            double opacity = card.Opacity;
            double rotation = motion.Rotate.Angle;
            double translation = motion.Translate.Y;

            window.RequestClose();
            await context.WaitForClosingAsync(window);

            card.Opacity.Should().Be(opacity);
            motion.Rotate.Angle.Should().Be(rotation);
            motion.Translate.Y.Should().Be(translation);
            await context.CompleteAnimationsAsync();
            await showing.WaitAsync(context.Token);
            window.IsVisible.Should().BeFalse();
        });
    }

    [Fact]
    public async Task Click_WithVisibleAndQueuedNotifications_HidesAllAndOpensAtomicArt()
    {
        await DispatchAsync(typeof(GenerationNotificationTests), async () =>
        {
            await using NotificationPresentationTestContext context = new();
            Task[] presentations = Enumerable.Range(0, 4).Select(index => context.StartAsync($"Промпт {index}")).ToArray();
            await context.WaitForVisibleAsync(3);
            await context.CompleteAnimationsAsync();
            GenerationNotificationWindow clicked = context.Windows[0];
            Border card = GetCard(clicked);
            Point point = card.TranslatePoint(new Point(24, 24), clicked)
                ?? throw new InvalidOperationException("The card is not attached to its window.");

            clicked.MouseDown(point, MouseButton.Left);
            clicked.MouseUp(point, MouseButton.Left);
            await context.CompleteAnimationsAsync();
            await Task.WhenAll(presentations).WaitAsync(context.Token);

            context.Windows.Should().HaveCount(3);
            context.Windows.Should().OnlyContain(window => !window.IsVisible);
            context.WindowState.Verify(state => state.ShowAndActivate(), Times.Once);
        });
    }

    [Fact]
    public async Task CloseCommand_WithSeveralNotifications_ClosesOnlyOneWithoutOpeningAtomicArt()
    {
        await DispatchAsync(typeof(GenerationNotificationTests), async () =>
        {
            await using NotificationPresentationTestContext context = new();
            Task first = context.StartAsync("Первый");
            _ = context.StartAsync("Второй");
            await context.WaitForVisibleAsync(2);
            await context.CompleteAnimationsAsync();
            GenerationNotificationWindow closing = context.Windows[0];
            GenerationNotificationViewModel model = closing.DataContext as GenerationNotificationViewModel
                ?? throw new InvalidOperationException("The notification view model is missing.");

            model.CloseCommand.Execute(null);
            await context.CompleteAnimationsAsync();
            await first.WaitAsync(context.Token);

            closing.IsVisible.Should().BeFalse();
            context.Windows[1].IsVisible.Should().BeTrue();
            context.WindowState.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public async Task Pointer_WithAnyNotification_RestartsTheTimersForAllNotifications()
    {
        await DispatchAsync(typeof(GenerationNotificationTests), async () =>
        {
            await using NotificationPresentationTestContext context = new();
            Task first = context.StartAsync("Первый");
            Task second = context.StartAsync("Второй");
            await context.WaitForVisibleAsync(2);
            await context.Clock.WaitForTimerCountAsync(2, context.Token);
            await context.CompleteAnimationsAsync();
            NotificationTestTimer[] previous = context.Clock.Timers.ToArray();
            GenerationNotificationWindow hovered = context.Windows[0];
            Border card = GetCard(hovered);
            Point point = card.TranslatePoint(new Point(24, 24), hovered)
                ?? throw new InvalidOperationException("The card is not attached to its window.");

            hovered.MouseMove(point);
            await Task.WhenAll(previous.Select(timer => timer.Disposed)).WaitAsync(context.Token);
            foreach (NotificationTestTimer timer in previous)
            {
                timer.Fire();
            }

            first.IsCompleted.Should().BeFalse();
            second.IsCompleted.Should().BeFalse();
            hovered.MouseMove(new Point(0, 0));
            await context.Clock.WaitForTimerCountAsync(4, context.Token);
            foreach (NotificationTestTimer timer in context.Clock.Timers.Skip(2))
            {
                timer.DueTime.Should().Be(TimeSpan.FromSeconds(5));
                timer.Fire();
            }

            await context.CompleteAnimationsAsync();
            await Task.WhenAll(first, second).WaitAsync(context.Token);
            context.Windows.Should().OnlyContain(window => !window.IsVisible);
        });
    }

    private static Border GetCard(GenerationNotificationWindow window)
    {
        return window.FindControl<Border>("NotificationCard")
            ?? throw new InvalidOperationException("The notification card is missing.");
    }
}
