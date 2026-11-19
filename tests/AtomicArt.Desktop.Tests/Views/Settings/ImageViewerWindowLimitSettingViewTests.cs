using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

using FluentAssertions;
using SukiUI.Controls;
using Xunit;

using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Settings;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Tests.Views.Settings;

public sealed class ImageViewerWindowLimitSettingViewTests : DesktopControlTestBase
{
    [Fact]
    public async Task Label_WhenPressedAndReleased_KeepsPositionAndTogglesLimit()
    {
        await DispatchAsync(async () =>
        {
            ImageViewerWindowLimitSettingsTestContext context = new(new SettingsState());
            using ImageViewerWindowLimitSettingViewModel editor = context.CreateEditor();
            ImageViewerWindowLimitSettingView view = new() { DataContext = editor };
            Window window = Show(view);

            try
            {
                TextBlock label = view.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Classes.Contains("settings-label") && text.IsEffectivelyVisible);
                Rect initial = GetBounds(label, view);
                Point labelPoint = label.TranslatePoint(new Point(8d, label.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The setting label is not attached.");

                window.MouseDown(labelPoint, MouseButton.Left);
                window.CaptureRenderedFrame();
                await Task.Delay(TimeSpan.FromMilliseconds(160d));
                window.CaptureRenderedFrame();
                Rect pressed = GetBounds(label, view);
                window.MouseUp(labelPoint, MouseButton.Left);
                await (editor.ApplyCommand.ExecutionTask ?? Task.CompletedTask);
                window.CaptureRenderedFrame();

                pressed.Should().Be(initial);
                GetBounds(label, view).Should().Be(initial);
                context.Limit.Limit.Should().Be(0);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Checkbox_AfterCommittingNumericInput_DisablesInputAndRestoresRememberedLimit(bool commitWithEnter)
    {
        await DispatchAsync(async () =>
        {
            ImageViewerWindowLimitSettingsTestContext context = new(new SettingsState());
            using ImageViewerWindowLimitSettingViewModel editor = context.CreateEditor();
            ImageViewerWindowLimitSettingView view = new() { DataContext = editor };
            ComboBox comparisonInput = new()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new string[] { "16" },
                SelectedIndex = 0
            };
            TextBox nextInput = new();
            StackPanel content = new();
            content.Children.Add(comparisonInput);
            content.Children.Add(view);
            content.Children.Add(nextInput);
            Window window = Show(content);

            try
            {
                CheckBox checkbox = view.GetVisualDescendants().OfType<CheckBox>().Single();
                NumericUpDown input = view.GetVisualDescendants().OfType<NumericUpDown>().Single();
                TextBlock title = view.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Classes.Contains("settings-label") && text.IsEffectivelyVisible);
                checkbox.IsChecked.Should().BeTrue();
                input.IsEffectivelyEnabled.Should().BeTrue();
                input.Value.Should().Be(3);
                input.ShowButtonSpinner.Should().BeFalse();
                GlassCard numericBackground = input.GetVisualDescendants().OfType<GlassCard>()
                    .Single(card => card.Name == "PART_GlassCard");
                GlassCard comparisonBackground = comparisonInput.GetVisualDescendants().OfType<GlassCard>()
                    .Single(card => card.Name == "border");
                numericBackground.Bounds.Width.Should().BeLessThan(comparisonBackground.Bounds.Width);
                numericBackground.Bounds.Height.Should().BeApproximately(comparisonBackground.Bounds.Height, 1d);
                Border indicator = checkbox.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Name == "SwitchTrack");
                (input.TranslatePoint(new Point(0d, input.Bounds.Height / 2d), title)?.Y)
                    .Should().BeApproximately(title.Bounds.Height / 2d, 1d);
                (indicator.TranslatePoint(default, input)?.X).Should().BeGreaterThan(input.Bounds.Width);

                TextBox textBox = input.GetVisualDescendants().OfType<TextBox>().Single();
                textBox.Focus().Should().BeTrue();
                window.KeyTextInput("abc");
                textBox.Text.Should().Be("3");
                textBox.Text = "1";
                textBox.Text = "12";
                textBox.IsFocused.Should().BeTrue();
                context.Limit.Limit.Should().Be(3);

                if (commitWithEnter)
                {
                    window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                }
                else
                {
                    nextInput.Focus().Should().BeTrue();
                }

                await (editor.ApplyCommand.ExecutionTask ?? Task.CompletedTask);
                context.Limit.Limit.Should().Be(12);

                Point titlePoint = title.TranslatePoint(new Point(title.Bounds.Width / 2d, title.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The setting title is not attached.");
                window.MouseDown(titlePoint, MouseButton.Left);
                window.MouseUp(titlePoint, MouseButton.Left);
                await (editor.ApplyCommand.ExecutionTask ?? Task.CompletedTask);
                window.CaptureRenderedFrame();
                input.IsEffectivelyEnabled.Should().BeFalse();
                input.Focus().Should().BeFalse();
                input.Value.Should().Be(12);
                context.Limit.Limit.Should().Be(0);

                checkbox.Focus().Should().BeTrue();
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                await (editor.ApplyCommand.ExecutionTask ?? Task.CompletedTask);
                input.IsEffectivelyEnabled.Should().BeTrue();
                input.Value.Should().Be(12);
                context.Limit.Limit.Should().Be(12);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Rect GetBounds(Control control, Control relativeTo)
    {
        Matrix transform = control.TransformToVisual(relativeTo)
            ?? throw new InvalidOperationException("The setting control is not attached.");
        return new Rect(control.Bounds.Size).TransformToAABB(transform);
    }
}
