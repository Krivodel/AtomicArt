using Avalonia.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class AtomicArtPromptDragDataTests
{
    [Fact]
    public void Create_WithPrompt_ProvidesInternalPromptAndPlainText()
    {
        const string Prompt = "Prompt from a gallery card";

        DataTransfer dataTransfer = AtomicArtPromptDragData.Create(Prompt);

        AtomicArtPromptDragData.IsPrompt(dataTransfer).Should().BeTrue();
        AtomicArtPromptDragData.GetPromptOrDefault(dataTransfer).Should().Be(Prompt);
        dataTransfer.TryGetText().Should().Be(Prompt);
    }

    [Fact]
    public void GetPromptOrDefault_WithExternalPlainText_ReturnsNull()
    {
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateText("External text"));

        string? prompt = AtomicArtPromptDragData.GetPromptOrDefault(dataTransfer);

        prompt.Should().BeNull();
    }
}
