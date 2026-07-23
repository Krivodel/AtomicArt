using System.Text;
using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation.GoogleInteractions;

public sealed class GoogleInteractionsStreamingTests
{
    private static readonly Guid LogicalGenerationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ReadAsByteArrayAsync_WithChunkedAttachment_WritesExactLengthAndValidBase64()
    {
        byte[] attachmentBytes = Enumerable
            .Range(0, 100003)
            .Select(value => (byte)(value % 251))
            .ToArray();
        TestAttachmentSource attachment = new(
            new GenerationAttachmentMetadataDto(
                0,
                "reference.png",
                GenerationImageContentTypes.Png,
                attachmentBytes.LongLength),
            attachmentBytes,
            maximumReadSize: 17);
        StreamingGenerationProviderContext context = CreateContext(attachment);
        using GoogleInteractionsStreamingContent content = new(context);

        byte[] serialized = await content.ReadAsByteArrayAsync();

        serialized.LongLength.Should().Be(content.Headers.ContentLength);
        using JsonDocument document = JsonDocument.Parse(serialized);
        string? base64 = document.RootElement
            .GetProperty("input")[1]
            .GetProperty("data")
            .GetString();
        Convert.FromBase64String(base64 ?? string.Empty)
            .Should()
            .Equal(attachmentBytes);
    }

    [Fact]
    public void Complete_WithLargeChunkedImageData_ExtractsMetadataWithoutRetainingImage()
    {
        string base64 = Convert.ToBase64String(new byte[1024 * 1024]);
        string responseJson = $$"""
        {
          "status": "completed",
          "output": [
            {
              "type": "image",
              "mime_type": "image/jpeg",
              "data": "{{base64}}"
            }
          ],
          "usage": {
            "total_input_tokens": 10,
            "total_output_tokens": 20,
            "total_tokens": 30
          }
        }
        """;
        GoogleStreamingResponseAnalyzer analyzer = new(
            new GoogleInteractionsResponseParser(),
            new GoogleInteractionsFailureClassifier());
        byte[] bytes = Encoding.UTF8.GetBytes(responseJson);

        for (int offset = 0; offset < bytes.Length; offset += 31)
        {
            int count = Math.Min(31, bytes.Length - offset);
            analyzer.Append(bytes.AsSpan(offset, count));
        }

        ProviderGenerationSummary summary = analyzer.Complete();

        summary.State.Should().Be("completed");
        summary.ResultCount.Should().Be(1);
        summary.ContentTypes.Should().ContainSingle()
            .Which.Should().Be(GenerationImageContentTypes.Jpeg);
        summary.Usage?.TotalTokens.Should().Be(30);
    }

    [Fact]
    public void Complete_WithExplicitInternalError_ClassifiesRetryableFailure()
    {
        string responseJson = """
        {
          "status": "failed",
          "error": {
            "status": "INTERNAL"
          }
        }
        """;
        GoogleStreamingResponseAnalyzer analyzer = new(
            new GoogleInteractionsResponseParser(),
            new GoogleInteractionsFailureClassifier());
        analyzer.Append(Encoding.UTF8.GetBytes(responseJson));

        Action act = () => analyzer.Complete();

        GoogleInteractionsException exception = act.Should()
            .Throw<GoogleInteractionsException>()
            .Which;
        exception.FailureKind.Should().Be(
            ImageGenerationProviderFailureKind.InternalError);
        exception.Retryable.Should().BeTrue();
    }

    [Fact]
    public void Complete_WithMalformedJson_ReturnsNonRetryableProviderFailure()
    {
        GoogleStreamingResponseAnalyzer analyzer = new(
            new GoogleInteractionsResponseParser(),
            new GoogleInteractionsFailureClassifier());
        analyzer.Append("{\"status\":\"completed\""u8);

        Action act = () => analyzer.Complete();

        GoogleInteractionsException exception = act.Should()
            .Throw<GoogleInteractionsException>()
            .Which;
        exception.FailureKind.Should().Be(
            ImageGenerationProviderFailureKind.InvalidResponse);
        exception.Retryable.Should().BeFalse();
    }

    [Fact]
    public void Complete_WithStructureDeeperThanLimit_ReturnsInvalidResponse()
    {
        GoogleStreamingResponseAnalyzer analyzer = new(
            new GoogleInteractionsResponseParser(),
            new GoogleInteractionsFailureClassifier(),
            maximumStructureDepth: 2);
        analyzer.Append("{\"value\":{\"nested\":{\"deep\":true}}}"u8);

        Action act = () => analyzer.Complete();

        act.Should()
            .Throw<GoogleInteractionsException>()
            .Which.FailureKind.Should()
            .Be(ImageGenerationProviderFailureKind.InvalidResponse);
    }

    [Fact]
    public void Complete_WithDiagnosticTextLongerThanLimit_ReturnsInvalidResponse()
    {
        GoogleStreamingResponseAnalyzer analyzer = new(
            new GoogleInteractionsResponseParser(),
            new GoogleInteractionsFailureClassifier(),
            maximumDiagnosticTextCharacters: 4);
        analyzer.Append("{\"status\":\"completed\"}"u8);

        Action act = () => analyzer.Complete();

        act.Should()
            .Throw<GoogleInteractionsException>()
            .Which.FailureKind.Should()
            .Be(ImageGenerationProviderFailureKind.InvalidResponse);
    }

    private static StreamingGenerationProviderContext CreateContext(
        IGenerationAttachmentSource attachment)
    {
        Dictionary<string, JsonElement> parameters =
            new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        StreamingImageGenerationRequest request = new(
            LogicalGenerationId,
            1,
            ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            "Create an image",
            "16:9",
            "2K",
            1.0,
            "low",
            parameters,
            new List<IGenerationAttachmentSource>
            {
                attachment
            });

        return new StreamingGenerationProviderContext(
            request,
            GenerationProviderIds.Google,
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().ProviderModelId,
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().Pricing,
            TestGenerationCredentials.ProviderCredential);
    }

    private sealed class TestAttachmentSource : IGenerationAttachmentSource
    {
        public GenerationAttachmentMetadataDto Metadata { get; }

        private readonly byte[] _content;
        private readonly int _maximumReadSize;

        public TestAttachmentSource(
            GenerationAttachmentMetadataDto metadata,
            byte[] content,
            int maximumReadSize)
        {
            Metadata = metadata;
            _content = content;
            _maximumReadSize = maximumReadSize;
        }

        public ValueTask<Stream> OpenReadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Stream stream = new ChunkedMemoryStream(
                _content,
                _maximumReadSize);

            return ValueTask.FromResult(stream);
        }
    }

    private sealed class ChunkedMemoryStream : MemoryStream
    {
        private readonly int _maximumReadSize;

        public ChunkedMemoryStream(
            byte[] content,
            int maximumReadSize)
            : base(content, writable: false)
        {
            _maximumReadSize = maximumReadSize;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return base.ReadAsync(
                buffer[..Math.Min(buffer.Length, _maximumReadSize)],
                cancellationToken);
        }
    }
}
