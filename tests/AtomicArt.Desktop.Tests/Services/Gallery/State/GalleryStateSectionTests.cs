using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Tests.TestDoubles;

namespace AtomicArt.Desktop.Tests.Services.Gallery.State;

public sealed class GalleryStateSectionTests
{
    private static readonly Guid ItemId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 7, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SerializePayload_WithThumbnailPath_WritesThumbnailPath()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        GalleryState state = new()
        {
            Items = [CreateState("image.png", "thumbnail.png")]
        };

        string json = JsonSerializer.Serialize(state, options);

        json.Should().Contain("\"thumbnailPath\":\"thumbnail.png\"");
    }

    [Fact]
    public void DeserializePayload_WithoutThumbnailPath_RestoresItem()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        GalleryStateSection section = new();
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "items": [
                {
                  "id": "55555555-5555-5555-5555-555555555555",
                  "modelId": "nano-banana-2",
                  "modelDisplayName": "Nano Banana 2",
                  "prompt": "Prompt",
                  "aspectRatio": "Авто",
                  "resolution": "1024x1024",
                  "createdAtUtc": "2026-07-07T09:00:00Z",
                  "status": "Generated",
                  "imagePath": "image.png",
                  "attachedImagesCount": 0
                }
              ]
            }
            """);

        object payload = section.DeserializePayload(
            section.SchemaVersion,
            document.RootElement,
            options);

        GalleryState state = payload.Should().BeOfType<GalleryState>().Subject;
        state.Items.Should().ContainSingle();
        state.Items[0].ThumbnailPath.Should().BeNull();
    }

    [Fact]
    public void NormalizeForDeserialization_WithUntrustedThumbnailPath_DropsThumbnailPath()
    {
        GalleryItemState state = CreateState("image.png", "thumbnail.png");

        GalleryItemState normalized = GalleryItemStateMapper.NormalizeForRestore(
            state,
            item => item.ImagePath,
            _ => null);

        normalized.ImagePath.Should().Be("image.png");
        normalized.ThumbnailPath.Should().BeNull();
    }

    private static GalleryItemState CreateState(string? imagePath, string? thumbnailPath)
    {
        return GalleryItemStateTestFactory.CreateGenerated(
            prompt: "Prompt",
            id: ItemId,
            createdAtUtc: CreatedAtUtc,
            imagePath: imagePath,
            thumbnailPath: thumbnailPath);
    }
}
