using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Behaviors;

public sealed class Dlss5ClipboardPasteBehaviorTests : DesktopControlTestBase
{
    [Fact]
    public async Task CtrlVFromHiddenTextBox_InvokesImageCommand()
    {
        await DispatchAsync(async () =>
        {
            Grid root = new();
            TextBox hiddenTextBox = new() { IsVisible = false };
            root.Children.Add(hiddenTextBox);
            TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            AsyncRelayCommand command = new(() =>
            {
                completion.TrySetResult(true);
                return Task.CompletedTask;
            });
            Dlss5ClipboardPasteBehavior.SetPasteCommand(root, command);
            Dlss5ClipboardPasteBehavior.SetIsEnabled(root, true);
            Window window = Show(root);

            try
            {
                hiddenTextBox.RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Key = Key.V,
                    KeyModifiers = KeyModifiers.Control,
                    Source = hiddenTextBox
                });

                bool invoked = await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));
                invoked.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void WindowPaste_RespectsPanelVisibilityAndTextFocus(bool isPanelOpen, bool isTextInput, bool expected)
    {
        Dispatch(() =>
        {
            Control focusedControl = isTextInput ? new TextBox() : new Button();
            bool invoked = false;
            AsyncRelayCommand command = new(() =>
            {
                invoked = true;
                return Task.CompletedTask;
            });
            Window window = Show(focusedControl);
            Dlss5ClipboardPasteBehavior.SetPasteCommand(window, command);
            Dlss5ClipboardPasteBehavior.SetIsEnabled(window, isPanelOpen);
            try
            {
                focusedControl.Focus();
                focusedControl.RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Key = Key.V,
                    KeyModifiers = KeyModifiers.Control,
                    Source = focusedControl
                });
                invoked.Should().Be(expected);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
