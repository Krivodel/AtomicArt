using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

using FluentAssertions;
using SukiUI.Controls;
using Xunit;

using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Resources;

public sealed class CheckBoxStylesTests : DesktopControlTestBase
{
    [Theory]
    [InlineData("")]
    [InlineData("settings-label")]
    public async Task Click_OnLabelIndicatorAndEmptySpace_TogglesOncePerClick(string classes)
    {
        await DispatchAsync(() =>
        {
            CheckBox checkBox = new() { Content = "Example option", IsChecked = false };
            checkBox.Classes.AddRange(classes.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            Window window = Show(checkBox);

            try
            {
                int changeCount = 0;
                checkBox.PropertyChanged += (_, args) =>
                {
                    if (args.Property == CheckBox.IsCheckedProperty)
                    {
                        changeCount++;
                    }
                };
                checkBox.Bounds.Width.Should().Be(window.ClientSize.Width);
                GlassCard indicator = checkBox.GetVisualDescendants().OfType<GlassCard>()
                    .Single(card => card.Name == "border");
                ContentPresenter label = checkBox.GetVisualDescendants().OfType<ContentPresenter>()
                    .Single(presenter => presenter.Name == "PART_ContentPresenter");
                label.Bounds.Left.Should().BeGreaterThan(indicator.Bounds.Right);

                string[] targets = ["label", "indicator", "space"];

                for (int index = 0; index < targets.Length; index++)
                {
                    CheckBoxTestInput.Click(window, checkBox, targets[index]);

                    checkBox.IsChecked.Should().Be(index % 2 == 0);
                    changeCount.Should().Be(index + 1);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("settings-label")]
    public async Task Click_WhenDisabled_KeepsValueForEveryTarget(string classes)
    {
        await DispatchAsync(() =>
        {
            CheckBox checkBox = new() { Content = "Disabled option", IsEnabled = false, IsChecked = false };
            checkBox.Classes.AddRange(classes.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            Window window = Show(checkBox);

            try
            {
                foreach (string target in new string[] { "label", "indicator", "space" })
                {
                    CheckBoxTestInput.Click(window, checkBox, target);

                    checkBox.IsChecked.Should().BeFalse();
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Space_WhenFocused_PreservesNativeKeyboardToggle()
    {
        await DispatchAsync(() =>
        {
            CheckBox checkBox = new() { Content = "Keyboard option", IsChecked = false };
            Window window = Show(checkBox);

            try
            {
                checkBox.Focus().Should().BeTrue();

                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);

                checkBox.IsChecked.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
