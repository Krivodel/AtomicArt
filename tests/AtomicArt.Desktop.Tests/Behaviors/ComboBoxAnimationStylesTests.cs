using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

namespace AtomicArt.Desktop.Tests.Behaviors;

public sealed class ComboBoxAnimationStylesTests
{
    private static readonly TimeSpan ExpectedDuration = TimeSpan.FromMilliseconds(150d);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<ComboBoxAnimationTestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public void GlobalResources_WhenLoaded_UsesOneHundredFiftyMillisecondDuration()
    {
        Dispatch(() =>
        {
            ComboBox comboBox = CreateComboBox();
            Window window = Show(comboBox);

            try
            {
                bool found = comboBox.TryFindResource(
                    "ComboBoxDropDownAnimationDuration",
                    out object? value);

                found.Should().BeTrue();
                value.Should().Be(ExpectedDuration);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task DropDownOpening_WhenAnimationRuns_UsesStandardScaleAndFade()
    {
        await DispatchAsync(async () =>
        {
            ComboBox comboBox = CreateComboBox();
            Window window = Show(comboBox);

            try
            {
                comboBox.IsDropDownOpen = true;
                await Task.Delay(TimeSpan.FromMilliseconds(50d));

                LayoutTransformControl panel = GetDropDownPanel(comboBox);
                ScaleTransform scale = panel.RenderTransform.Should()
                    .BeOfType<ScaleTransform>()
                    .Subject;
                panel.Opacity.Should().BeGreaterThan(0d).And.BeLessThan(1d);
                scale.ScaleX.Should().BeGreaterThan(0.92d);
                scale.ScaleY.Should().BeGreaterThan(0.72d);

                await Task.Delay(TimeSpan.FromMilliseconds(175d));

                panel.Opacity.Should().Be(1d);
                scale.ScaleX.Should().Be(1d);
                scale.ScaleY.Should().Be(1d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(0.6d)]
    [InlineData(1.5d)]
    public void DropDownOpening_WhenPlacementTargetIsScaled_InheritsScale(double uiScale)
    {
        Dispatch(() =>
        {
            ComboBox comboBox = CreateComboBox();
            LayoutTransformControl scaleHost = new()
            {
                Child = comboBox,
                LayoutTransform = new ScaleTransform(uiScale, uiScale)
            };
            Window window = Show(scaleHost);

            try
            {
                comboBox.IsDropDownOpen = true;

                Popup popup = GetPopup(comboBox);
                LayoutTransformControl panel = GetDropDownPanel(comboBox);
                PopupRoot popupRoot = TopLevel.GetTopLevel(panel).Should()
                    .BeOfType<PopupRoot>()
                    .Subject;
                ScaleTransform inheritedScale = popupRoot.Transform.Should()
                    .BeOfType<ScaleTransform>()
                    .Subject;

                popup.InheritsTransform.Should().BeTrue();
                inheritedScale.ScaleX.Should().BeApproximately(uiScale, 0.001d);
                inheritedScale.ScaleY.Should().BeApproximately(uiScale, 0.001d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void Dispatch(Action action)
    {
        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(
            typeof(ComboBoxAnimationStylesTests));
        session.Dispatch(action, CancellationToken.None);
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(
            typeof(ComboBoxAnimationStylesTests));
        await session.Dispatch(
            async () =>
            {
                await action();

                return true;
            },
            CancellationToken.None);
    }

    private static ComboBox CreateComboBox()
    {
        List<string> items = ["One", "Two", "Three", "Four"];

        return new ComboBox
        {
            Width = 180d,
            ItemsSource = items,
            SelectedIndex = 0
        };
    }

    private static Window Show(Control control)
    {
        Window window = new()
        {
            Width = 400d,
            Height = 400d,
            Content = control
        };
        window.Show();
        window.CaptureRenderedFrame();

        return window;
    }

    private static LayoutTransformControl GetDropDownPanel(ComboBox comboBox)
    {
        Popup popup = GetPopup(comboBox);

        return popup.Child as LayoutTransformControl
            ?? throw new InvalidOperationException("Drop-down panel was not found.");
    }

    private static Popup GetPopup(ComboBox comboBox)
    {
        return comboBox
            .GetVisualDescendants()
            .OfType<Popup>()
            .Single(control => string.Equals(
                control.Name,
                "PART_Popup",
                StringComparison.Ordinal));
    }
}
