using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationChatHistoryTests
{
    [Fact]
    public void Constructor_WhenInputCollectionsChange_KeepsMessagesAndReferenceHashesConsistent()
    {
        GenerationReference reference = new("image-hash", "image.png", GenerationImageContentTypes.Png);
        GenerationReference[] images = [reference];
        GenerationChatMessage original = new(
            true, "Original", new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc), images);
        GenerationChatMessage[] messages = [original];
        GenerationChatHistory history = new(messages);

        images[0] = reference with { Hash = "replacement-hash" };
        messages[0] = original with { Text = "Replacement" };

        history.Messages.Should().ContainSingle().Which.Text.Should().Be(original.Text);
        history.Messages[0].Images.Should().ContainSingle().Which.Should().Be(reference);
        history.ReferenceHashes.Should().Equal(reference.Hash);
    }
}
