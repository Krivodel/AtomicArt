using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation;

public sealed class FakeStreamingImageGenerationProviderTests
{
    private static readonly Guid LogicalGenerationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task CopyToAsync_WithFileBackedSource_StreamsCompleteImageInBoundedReads()
    {
        byte[] imageBytes = Enumerable
            .Range(0, 200003)
            .Select(value => (byte)(value % 251))
            .ToArray();
        TrackingReadStream sourceStream = new(imageBytes);
        TestStreamingPlaceholderImageProvider sourceProvider = new(
            new StreamingPlaceholderImage(
                GenerationImageContentTypes.Png,
                imageBytes.LongLength,
                sourceStream));
        FakeStreamingImageGenerationProvider provider = new(sourceProvider);
        StreamingGenerationProviderContext context = CreateContext();
        await using IProviderGenerationStream providerStream =
            await provider.CreateStreamAsync(
                context,
                CancellationToken.None);
        using MemoryStream destination = new();

        await providerStream.CopyToAsync(
            destination,
            1024L * 1024L,
            CancellationToken.None);

        using JsonDocument document = JsonDocument.Parse(
            destination.ToArray());
        string? base64 = document.RootElement
            .GetProperty("output")[0]
            .GetProperty("data")
            .GetString();
        Convert.FromBase64String(base64 ?? string.Empty)
            .Should()
            .Equal(imageBytes);
        sourceStream.MaximumRequestedReadLength.Should().BeLessThanOrEqualTo(
            49152);
    }

    private static StreamingGenerationProviderContext CreateContext()
    {
        GenerationModelMetadataDto metadata =
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        StreamingImageGenerationRequest request = new(
            LogicalGenerationId,
            1,
            TestGenerationModelCatalogAugmenter.ModelId,
            "Create an image",
            "1:1",
            "1K",
            1.0,
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            Array.Empty<IGenerationAttachmentSource>());

        return new StreamingGenerationProviderContext(
            request,
            GenerationProviderIds.Test,
            "test-folder",
            metadata.Pricing,
            null,
            metadata.TransportLimits);
    }

    private sealed class TestStreamingPlaceholderImageProvider
        : IStreamingPlaceholderImageProvider
    {
        private readonly StreamingPlaceholderImage _image;

        public TestStreamingPlaceholderImageProvider(
            StreamingPlaceholderImage image)
        {
            _image = image;
        }

        public Task<StreamingPlaceholderImage> OpenNextAsync(
            string modelId,
            int itemIndex,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            return Task.FromResult(_image);
        }
    }

    private sealed class TrackingReadStream : MemoryStream
    {
        public int MaximumRequestedReadLength { get; private set; }

        public TrackingReadStream(byte[] content)
            : base(content, writable: false)
        {
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            MaximumRequestedReadLength = Math.Max(
                MaximumRequestedReadLength,
                buffer.Length);

            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
