using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Behaviors;

public sealed class UnsignedIntegerInputBehaviorTests : DesktopControlTestBase
{
    [Theory]
    [InlineData("abc", false, "3")]
    [InlineData("ы", false, "3")]
    [InlineData("1a2", false, "3")]
    [InlineData("-12", false, "3")]
    [InlineData("1.2", false, "3")]
    [InlineData("12", false, "12")]
    [InlineData("abc", true, "3")]
    [InlineData("1a2", true, "3")]
    [InlineData("1,2", true, "3")]
    [InlineData("12\n34", true, "3")]
    [InlineData("12", true, "12")]
    public async Task Input_WithDigitFilter_RejectsInvalidTextAndAcceptsDigits(
        string text, bool paste, string expected)
    {
        await DispatchAsync(async () =>
        {
            NumericUpDown input = new() { Value = 3, ShowButtonSpinner = false };
            UnsignedIntegerInputBehavior.SetIsEnabled(input, true);
            Window window = Show(input);

            try
            {
                TextBox editor = input.GetVisualDescendants().OfType<TextBox>().Single();
                editor.Focus();
                editor.SelectAll();

                if (paste)
                {
                    IClipboard clipboard = window.Clipboard
                        ?? throw new InvalidOperationException("The test window has no clipboard.");
                    await clipboard.SetTextAsync(text);
                    editor.Paste();
                }
                else
                {
                    window.KeyTextInput(text);
                }

                editor.Text.Should().Be(expected);
                input.Text.Should().Be(expected);
                input.Value.Should().Be(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Input_WhenFilterIsRemoved_RetainsNormalEditingBehavior()
    {
        await DispatchAsync(() =>
        {
            NumericUpDown input = new() { Value = 3, ShowButtonSpinner = false };
            UnsignedIntegerInputBehavior.SetIsEnabled(input, true);
            UnsignedIntegerInputBehavior.SetIsEnabled(input, false);
            TextBox unrestricted = new();
            StackPanel content = new();
            content.Children.Add(input);
            content.Children.Add(unrestricted);
            Window window = Show(content);

            try
            {
                TextBox editor = input.GetVisualDescendants().OfType<TextBox>().Single();
                editor.Focus();
                editor.SelectAll();
                window.KeyTextInput("letters");
                editor.Text.Should().Be("letters");

                unrestricted.Focus();
                window.KeyTextInput("обычный текст");
                unrestricted.Text.Should().Be("обычный текст");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Input_WhenDeletingReplacingAndUndoing_PreservesStandardEditing()
    {
        await DispatchAsync(() =>
        {
            NumericUpDown input = new() { Value = 3, ShowButtonSpinner = false };
            UnsignedIntegerInputBehavior.SetIsEnabled(input, true);
            Window window = Show(input);

            try
            {
                TextBox editor = input.GetVisualDescendants().OfType<TextBox>().Single();
                editor.Focus();
                editor.SelectAll();
                window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
                editor.Text.Should().BeEmpty();

                window.KeyTextInput("12");
                editor.Text.Should().Be("12");
                editor.SelectAll();
                window.KeyTextInput("7");
                editor.Text.Should().Be("7");
                editor.Undo();
                editor.Text.Should().Be("12");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
