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
    public async Task Command_WhenEnterIsPressed_Executes()
    {
        await DispatchAsync(() =>
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Command_WhenTextBoxLosesFocus_Executes(bool multiline)
    {
        await DispatchAsync(() =>
        {
            int executionCount = 0;
            TextBox textBox = new() { AcceptsReturn = multiline };
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

    [Fact]
    public async Task Command_WhenEnterIsPressedInMultilineEditor_InsertsNewlineWithoutCommitting()
    {
        await DispatchAsync(() =>
        {
            const string FirstLine = "First line";
            int executionCount = 0;
            TextBox textBox = new() { AcceptsReturn = true, Text = FirstLine };
            SettingCommitBehavior.SetCommand(textBox, new RelayCommand(() => executionCount++));
            Window window = Show(textBox);

            try
            {
                textBox.Focus();
                textBox.CaretIndex = FirstLine.Length;

                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

                executionCount.Should().Be(0);
                textBox.Text.Should().StartWith(FirstLine).And.EndWith("\n");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
