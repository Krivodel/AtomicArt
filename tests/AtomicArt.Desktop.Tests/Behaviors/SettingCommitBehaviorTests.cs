using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Behaviors;

public sealed class SettingCommitBehaviorTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void Command_WhenEnterIsPressed_Executes()
    {
        Dispatch(() =>
        {
            int executionCount = 0;
            TextBox textBox = new();
            SettingCommitBehavior.SetCommand(
                textBox,
                new RelayCommand(() => executionCount++));
            Window window = Show(textBox);

            try
            {
                textBox.Focus();

                window.KeyPress(
                    Key.Enter,
                    RawInputModifiers.None,
                    PhysicalKey.Enter,
                    null);

                executionCount.Should().Be(1);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Command_WhenTextBoxLosesFocus_Executes()
    {
        Dispatch(() =>
        {
            int executionCount = 0;
            TextBox textBox = new();
            TextBox nextTextBox = new();
            SettingCommitBehavior.SetCommand(
                textBox,
                new RelayCommand(() => executionCount++));
            StackPanel panel = new();
            panel.Children.Add(textBox);
            panel.Children.Add(nextTextBox);
            Window window = Show(panel);

            try
            {
                textBox.Focus();

                nextTextBox.Focus();

                executionCount.Should().Be(1);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
