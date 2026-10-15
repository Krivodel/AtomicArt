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
                    .Single(block => block.Classes.Contains("settings-label"));
                checkbox.IsChecked.Should().BeTrue();
                input.IsEffectivelyEnabled.Should().BeTrue();
                input.Value.Should().Be(3);
                input.ShowButtonSpinner.Should().BeFalse();
                GlassCard numericBackground = input.GetVisualDescendants().OfType<GlassCard>()
                    .Single(card => card.Name == "PART_GlassCard");
                GlassCard comparisonBackground = comparisonInput.GetVisualDescendants().OfType<GlassCard>()
                    .Single(card => card.Name == "border");
                numericBackground.Bounds.Width.Should().BeApproximately(comparisonBackground.Bounds.Width, 1d);
                numericBackground.Bounds.Height.Should().BeApproximately(comparisonBackground.Bounds.Height, 1d);
                Point? checkboxPosition = checkbox.TranslatePoint(default, title);
                checkboxPosition.Should().NotBeNull();
                checkboxPosition?.X.Should().BeGreaterThan(title.Bounds.Width);

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

                checkbox.SetCurrentValue(CheckBox.IsCheckedProperty, false);
                await (editor.ApplyCommand.ExecutionTask ?? Task.CompletedTask);
                window.CaptureRenderedFrame();
                input.IsEffectivelyEnabled.Should().BeFalse();
                input.Focus().Should().BeFalse();
                input.Value.Should().Be(12);
                context.Limit.Limit.Should().Be(0);

                checkbox.SetCurrentValue(CheckBox.IsCheckedProperty, true);
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
}
