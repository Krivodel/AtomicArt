using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Resources;

public sealed class Dlss5StylesTests : DesktopControlTestBase
{
    [Fact]
    public void PlaceholderBrush_UsesBlueCyanVioletGradient()
    {
        Dispatch(() =>
        {
            Border control = new();
            Window window = Show(control);

            try
            {
                LinearGradientBrush brush = GetResource<LinearGradientBrush>(
                    control,
                    "Dlss5PlaceholderBrush");

                brush.GradientStops.Should().HaveCount(3);
                brush.GradientStops[0].Color.Should().Be(Color.Parse("#173C66"));
                brush.GradientStops[1].Color.Should().Be(Color.Parse("#1D526F"));
                brush.GradientStops[2].Color.Should().Be(Color.Parse("#472A68"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task IconButton_PointerOver_UsesAccentWithoutChangingGeometry()
    {
        await DispatchAsync(async () =>
        {
            Button button = new() { Content = new Border() };
            Window window = Show(button, 120, 120);

            try
            {
                button.Theme = GetResource<ControlTheme>(button, "Dlss5IconButtonTheme");
                window.CaptureRenderedFrame();
                Rect boundsBefore = button.Bounds;
                ITransform? transformBefore = button.RenderTransform;
                Point center = GetCenter(button, window);

                window.MouseMove(center, RawInputModifiers.None);
                await Task.Delay(250);

                Border surface = GetTemplateBorder(button, "Surface");
                Color expectedColor = GetResource<ISolidColorBrush>(button, "Dlss5IconHoverBrush").Color;
                surface.Background.Should().BeAssignableTo<ISolidColorBrush>()
                    .Which.Color.Should().Be(expectedColor);
                button.Bounds.Should().Be(boundsBefore);
                button.RenderTransform.Should().BeSameAs(transformBefore);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ImageButton_Press_DoesNotMoveOrScale()
    {
        await DispatchAsync(async () =>
        {
            Button button = new()
            {
                Width = 180,
                Height = 120,
                Content = new Border { Background = Brushes.CadetBlue }
            };
            Window window = Show(button, 240, 180);

            try
            {
                button.Theme = GetResource<ControlTheme>(button, "Dlss5ImageButtonTheme");
                window.CaptureRenderedFrame();
                Point center = GetCenter(button, window);
                Rect boundsBefore = button.Bounds;
                ITransform? transformBefore = button.RenderTransform;

                window.MouseMove(center, RawInputModifiers.None);
                window.MouseDown(center, MouseButton.Left);
                await Task.Delay(50);

                button.IsPressed.Should().BeTrue();
                button.Bounds.Should().Be(boundsBefore);
                button.RenderTransform.Should().BeSameAs(transformBefore);

                window.MouseUp(center, MouseButton.Left);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static T GetResource<T>(Control control, string key)
        where T : class
    {
        control.TryFindResource(key, out object? value).Should().BeTrue();

        return value.Should().BeAssignableTo<T>().Subject;
    }

    private static Border GetTemplateBorder(Button button, string name)
    {
        return button
            .GetVisualDescendants()
            .OfType<Border>()
            .Single(border => string.Equals(border.Name, name, StringComparison.Ordinal));
    }

    private static Point GetCenter(Control control, Visual relativeTo)
    {
        Point origin = control.TranslatePoint(default, relativeTo)
            ?? throw new InvalidOperationException("Control position was not found.");

        return origin + new Vector(control.Bounds.Width / 2, control.Bounds.Height / 2);
    }
}
