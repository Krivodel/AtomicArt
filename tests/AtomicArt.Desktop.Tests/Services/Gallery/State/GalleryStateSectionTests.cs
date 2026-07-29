using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;

namespace AtomicArt.Desktop.Tests.Services.Gallery.State;

public sealed class GalleryStateSectionTests
{
    private static readonly Guid ItemId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 7, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SchemaVersion_WithFailureCode_ReturnsVersionFour()
    {
        GalleryStateSection section = new();

        int schemaVersion = section.SchemaVersion;

        schemaVersion.Should().Be(4);
    }

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
    public void SerializePayload_WithGalleryOrderTimestamp_WritesTimestamp()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        GalleryState state = new()
        {
            Items =
            [
                GalleryItemStateTestFactory.CreateGenerated(
                    id: ItemId,
                    galleryOrderTimestampUtc: CreatedAtUtc)
            ]
        };

        string json = JsonSerializer.Serialize(state, options);

        json.Should().Contain(
            "\"galleryOrderTimestampUtc\":\"2026-07-07T09:00:00Z\"");
    }

    [Fact]
    public void SerializePayload_WithFailedItem_WritesCodeWithoutMessage()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        GalleryState state = new()
        {
            Items = [CreateFailedState()]
        };

        string json = JsonSerializer.Serialize(state, options);

        json.Should().Contain(
            $"\"failureCode\":\"{GenerationProviderFailureErrorCodes.RequestRejected}\"");
        json.Should().NotContain("failureMessage");
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
        state.Items[0].GalleryOrderTimestampUtc.Should().BeNull();
    }

    [Fact]
    public void DeserializePayload_WithLegacyFailureMessage_ReplacesTextWithUnknownCode()
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
                  "status": "Failed",
                  "failureMessage": "Старый сохранённый текст.",
                  "attachedImagesCount": 0
                }
              ]
            }
            """);

        object payload = section.DeserializePayload(
            3,
            document.RootElement,
            options);

        GalleryState state = payload.Should().BeOfType<GalleryState>().Subject;
        state.Items.Should().ContainSingle();
        state.Items[0].FailureCode.Should().Be(GenerationClientFailureCodes.Unknown);
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

    private static GalleryItemState CreateFailedState()
    {
        return new GalleryItemState
        {
            Id = ItemId,
            ModelId = "nano-banana-2",
            ModelDisplayName = "Nano Banana 2",
            Prompt = "Prompt",
            AspectRatio = "Авто",
            Resolution = "1024x1024",
            CreatedAtUtc = CreatedAtUtc,
            Status = GenerationItemStatus.Failed,
            FailureCode = GenerationProviderFailureErrorCodes.RequestRejected
        };
    }
}
