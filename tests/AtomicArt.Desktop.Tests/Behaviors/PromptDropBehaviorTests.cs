using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;

using CommunityToolkit.Mvvm.Input;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Behaviors;

public sealed class PromptDropBehaviorTests : AnimatedGalleryControlTestBase
{
    private static readonly Point TargetCenter = new(160d, 90d);

    [Fact]
    public void DragOver_WithAtomicArtPrompt_ActivatesOverlay()
    {
        Dispatch(() =>
        {
            RelayCommand<string?> command = new(_ => { });
            ImageDropOverlayControl overlay = new();
            Border target = CreateTarget(command);
            PromptDropBehavior.SetOverlay(target, overlay);
            Window window = Show(target, 320d, 180d);

            try
            {
                DataTransfer dataTransfer = AtomicArtPromptDragData.Create(
                    "Prompt from a gallery card");

                RaiseDragDrop(window, RawDragEventType.DragOver, dataTransfer);

                overlay.IsActive.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Drop_WithAtomicArtPrompt_ExecutesReplaceCommand()
    {
        Dispatch(() =>
        {
            const string prompt = "Prompt from a gallery card";
            string? replacedPrompt = null;
            RelayCommand<string?> command = new(value => replacedPrompt = value);
            Border target = CreateTarget(command);
            Window window = Show(target, 320d, 180d);

            try
            {
                DataTransfer dataTransfer = AtomicArtPromptDragData.Create(prompt);

                RaiseDragDrop(window, RawDragEventType.Drop, dataTransfer);

                replacedPrompt.Should().Be(prompt);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Drop_WithExternalPlainText_DoesNotExecuteReplaceCommand()
    {
        Dispatch(() =>
        {
            string? replacedPrompt = null;
            RelayCommand<string?> command = new(value => replacedPrompt = value);
            Border target = CreateTarget(command);
            Window window = Show(target, 320d, 180d);

            try
            {
                DataTransfer dataTransfer = new();
                dataTransfer.Add(DataTransferItem.CreateText("External text"));

                RaiseDragDrop(window, RawDragEventType.Drop, dataTransfer);

                replacedPrompt.Should().BeNull();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Border CreateTarget(IRelayCommand<string?> command)
    {
        Border target = new()
        {
            Width = 320d,
            Height = 180d,
            Background = Brushes.Transparent
        };
        PromptDropBehavior.SetReplacePromptCommand(target, command);
        PromptDropBehavior.SetIsEnabled(target, true);

        return target;
    }

    private static void RaiseDragDrop(
        Window window,
        RawDragEventType eventType,
        DataTransfer dataTransfer)
    {
        window.DragDrop(
            TargetCenter,
            eventType,
            dataTransfer,
            DragDropEffects.Copy,
            RawInputModifiers.None);
    }
}
