using System.Net;
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
    private const int MaximumFilteredResponseBytes = 4 * 1024 * 1024;
    private const int MaximumStructureDepth = 64;
    private const int MaximumDiagnosticTextCharacters = 512;
    private const int ResponseBufferSize = 65536;

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
        using GoogleInteractionsStreamingContent content = new(
            context,
            CreateOptions());

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
    public async Task ReadAsByteArrayAsync_WithConfiguredRequestProperties_WritesConfiguredValues()
    {
        byte[] attachmentBytes = [1, 2, 3];
        TestAttachmentSource attachment = new(
            new GenerationAttachmentMetadataDto(
                0,
                "reference.png",
                GenerationImageContentTypes.Png,
                attachmentBytes.LongLength),
            attachmentBytes,
            maximumReadSize: 3);
        StreamingGenerationProviderContext context = CreateContext(attachment);
        GoogleInteractionsOptions options = new()
        {
            Base64InputBufferSize = 3,
            Base64OutputBufferSize = 4,
            ServiceTier = "configured-tier",
            StoreInteractions = false
        };
        using GoogleInteractionsStreamingContent content = new(
            context,
            options);

        byte[] serialized = await content.ReadAsByteArrayAsync();

        using JsonDocument document = JsonDocument.Parse(serialized);
        JsonElement root = document.RootElement;
        root.GetProperty("service_tier").GetString()
            .Should().Be(options.ServiceTier);
        root.GetProperty("store").GetBoolean()
            .Should().Be(options.StoreInteractions);
        root.GetProperty("system_instruction").GetString()
            .Should().Be(
                GoogleInteractionsImageOutputContract.SystemInstruction);
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
        GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer();
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
    public void Complete_WithEveryPossibleBoundaryAndEscapedData_ExtractsAllMetadata()
    {
        string responseJson = """
        {
          "state": "succeeded",
          "steps": [
            {
              "type": "thought",
              "signature": "opaque\\\"signature"
            }
          ],
          "outputImages": [
            {
              "type": "image",
              "mime_type": "image/jpeg",
              "data": "not-base64\\\"first"
            },
            {
              "type": "image",
              "mimeType": "image/jpeg",
              "data": "second\\\\value"
            }
          ],
          "usage": {
            "totalInputTokens": 10,
            "totalOutputTokens": 20,
            "totalTokens": 30,
            "outputTokensByModality": [
              {
                "modality": "image",
                "tokenCount": 20
              }
            ]
          }
        }
        """;
        byte[] bytes = Encoding.UTF8.GetBytes(responseJson);

        for (int boundary = 0; boundary <= bytes.Length; boundary++)
        {
            GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer();
            analyzer.Append(bytes.AsSpan(0, boundary));
            analyzer.Append(bytes.AsSpan(boundary));

            ProviderGenerationSummary summary = analyzer.Complete();

            summary.State.Should().Be("succeeded");
            summary.ResultCount.Should().Be(2);
            summary.ContentTypes.Should().OnlyContain(
                contentType => contentType == GenerationImageContentTypes.Jpeg);
            summary.Usage.Should().BeEquivalentTo(new
            {
                TotalInputTokens = 10,
                TotalOutputTokens = 20,
                TotalTokens = 30,
                OutputTokensByModality = new[]
                {
                    new
                    {
                        Modality = GenerationUsageModalityNames.Image,
                        Tokens = 20
                    }
                }
            });
        }
    }

    [Theory]
    [InlineData("""{"data":"unfinished""")]
    [InlineData("""{"data":"unfinished\""")]
    [InlineData("""{"data""")]
    [InlineData("""{"data":""")]
    public void Complete_WithTruncatedDataBoundary_ReturnsInvalidResponse(
        string responseJson)
    {
        GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer();
        analyzer.Append(Encoding.UTF8.GetBytes(responseJson));

        Action act = () => analyzer.Complete();

        GoogleInteractionsException exception = act.Should()
            .Throw<GoogleInteractionsException>()
            .Which;
        exception.FailureKind.Should().Be(
            ImageGenerationProviderFailureKind.InvalidResponse);
        exception.Retryable.Should().BeFalse();
    }

    [Fact]
    public void Complete_WithImageLargerThanMetadataLimit_ExtractsMetadata()
    {
        string base64 = Convert.ToBase64String(new byte[2 * 1024 * 1024]);
        string responseJson = CreateCompletedResponse(base64);
        GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer(
            maximumFilteredResponseBytes: 512);
        byte[] bytes = Encoding.UTF8.GetBytes(responseJson);

        for (int offset = 0; offset < bytes.Length; offset += 65536)
        {
            int count = Math.Min(65536, bytes.Length - offset);
            analyzer.Append(bytes.AsSpan(offset, count));
        }

        ProviderGenerationSummary summary = analyzer.Complete();

        summary.ResultCount.Should().Be(1);
    }

    [Fact]
    public void Append_WithMetadataLargerThanLimit_ReturnsInvalidResponse()
    {
        GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer(
            maximumFilteredResponseBytes: 64);
        byte[] bytes = Encoding.UTF8.GetBytes(
            $$"""{"diagnostic":"{{new string('a', 65)}}"}""");

        Action act = () => analyzer.Append(bytes);

        act.Should()
            .Throw<GoogleInteractionsException>()
            .Which.FailureKind.Should()
            .Be(ImageGenerationProviderFailureKind.InvalidResponse);
    }

    [Fact]
    public void AppendAndSanitize_WithEveryPossibleBoundary_RemovesSignatureOnly()
    {
        const string Signature = "opaque\\\\\\\"signature";
        const string Data = "not-base64\\\\\\\"payload";
        string responseJson = $$"""
        {
          "status": "completed",
          "steps": [
            {
              "type": "thought",
              "signature": "{{Signature}}"
            }
          ],
          "output": [
            {
              "type": "image",
              "mime_type": "image/jpeg",
              "data": "{{Data}}"
            }
          ],
          "usage": {
            "total_input_tokens": 10,
            "total_output_tokens": 20,
            "total_tokens": 30
          }
        }
        """;
        string expectedResponse = responseJson.Replace(
            Signature,
            string.Empty,
            StringComparison.Ordinal);
        byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);

        for (int boundary = 0; boundary <= responseBytes.Length; boundary++)
        {
            GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer();
            byte[] workingBytes = responseBytes.ToArray();
            using MemoryStream destination = new();

            int firstLength = analyzer.AppendAndSanitize(
                workingBytes.AsSpan(0, boundary));
            destination.Write(workingBytes.AsSpan(0, firstLength));
            int secondLength = analyzer.AppendAndSanitize(
                workingBytes.AsSpan(boundary));
            destination.Write(
                workingBytes.AsSpan(boundary, secondLength));

            ProviderGenerationSummary summary = analyzer.Complete();

            Encoding.UTF8.GetString(destination.ToArray())
                .Should().Be(expectedResponse);
            summary.ResultCount.Should().Be(1);
        }
    }

    [Fact]
    public async Task CopyToAsync_WithSignature_WritesSanitizedResponse()
    {
        const string Signature = "opaque\\\\\\\"signature";
        string responseJson = $$"""
        {
          "status": "completed",
          "steps": [
            {
              "type": "thought",
              "signature": "{{Signature}}"
            }
          ],
          "output": [
            {
              "type": "image",
              "mime_type": "image/jpeg",
              "data": "not-base64\\\"payload"
            }
          ],
          "usage": {
            "total_input_tokens": 10,
            "total_output_tokens": 20,
            "total_tokens": 30
          }
        }
        """;
        string expectedResponse = responseJson.Replace(
            Signature,
            string.Empty,
            StringComparison.Ordinal);
        byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
        using HttpResponseMessage responseMessage = new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(responseBytes)
        };
        Stream responseContent = new ChunkedMemoryStream(
            responseBytes,
            maximumReadSize: 7);
        GoogleInteractionsStreamingResponse response = new(
            responseMessage,
            responseContent);
        await using GoogleProviderGenerationStream providerStream = new(
            response,
            new GoogleInteractionsResponseParser(),
            new GoogleInteractionsFailureClassifier(),
            maximumProviderResponseBytes: responseBytes.Length,
            maximumAnalyzedMetadataBytes: 512,
            maximumStructureDepth: 64,
            maximumDiagnosticTextCharacters: 512,
            responseBufferSize: ResponseBufferSize);
        using MemoryStream destination = new();

        await providerStream.CopyToAsync(
            destination,
            responseBytes.Length,
            CancellationToken.None);

        Encoding.UTF8.GetString(destination.ToArray())
            .Should().Be(expectedResponse);
        providerStream.Summary?.ResultCount.Should().Be(1);
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
        GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer();
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
        GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer();
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
        GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer(
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
        GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer(
            maximumDiagnosticTextCharacters: 4);
        analyzer.Append("{\"status\":\"completed\"}"u8);

        Action act = () => analyzer.Complete();

        act.Should()
            .Throw<GoogleInteractionsException>()
            .Which.FailureKind.Should()
            .Be(ImageGenerationProviderFailureKind.InvalidResponse);
    }

    [Fact]
    public void Complete_WithLongErrorMessage_ReturnsInvalidResponse()
    {
        GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer(
            maximumDiagnosticTextCharacters: 4);
        analyzer.Append(
            """{"error":{"message":"provider diagnostic"}}"""u8);

        Action act = () => analyzer.Complete();

        act.Should()
            .Throw<GoogleInteractionsException>()
            .Which.FailureKind.Should()
            .Be(ImageGenerationProviderFailureKind.InvalidResponse);
    }

    [Fact]
    public void Complete_WithLongModelTextAndImage_ExtractsImageMetadata()
    {
        string modelText = new('т', 1580);
        string responseJson = $$"""
        {
          "status": "completed",
          "steps": [
            {
              "type": "model_output",
              "content": [
                {
                  "type": "text",
                  "text": "{{modelText}}"
                }
              ]
            },
            {
              "type": "model_output",
              "content": [
                {
                  "type": "image",
                  "mime_type": "image/jpeg",
                  "data": "not-base64"
                }
              ]
            }
          ],
          "usage": {
            "total_input_tokens": 10,
            "total_output_tokens": 20,
            "total_tokens": 30
          }
        }
        """;
        GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer(
            maximumDiagnosticTextCharacters: 512);
        byte[] bytes = Encoding.UTF8.GetBytes(responseJson);

        for (int offset = 0; offset < bytes.Length; offset += 23)
        {
            int count = Math.Min(23, bytes.Length - offset);
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
    public void Complete_WithLongModelTextWithoutImage_ReturnsInvalidResponse()
    {
        string modelText = new('т', 1580);
        string responseJson = $$"""
        {
          "status": "completed",
          "steps": [
            {
              "type": "model_output",
              "content": [
                {
                  "type": "text",
                  "text": "{{modelText}}"
                }
              ]
            }
          ]
        }
        """;
        GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer(
            maximumDiagnosticTextCharacters: 512);
        analyzer.Append(Encoding.UTF8.GetBytes(responseJson));

        Action act = () => analyzer.Complete();

        GoogleInteractionsException exception = act.Should()
            .Throw<GoogleInteractionsException>()
            .Which;
        exception.FailureKind.Should().Be(
            ImageGenerationProviderFailureKind.InvalidResponse);
        exception.NoImageDiagnostics.Should().BeEquivalentTo(new
        {
            Category = "text_only",
            TextContentLength = modelText.Length,
            TextContentItemCount = 1
        });
    }

    [Fact]
    public void Complete_WithLongThoughtSignature_DoesNotRetainSignature()
    {
        string signature = new('s', 4096);
        string responseJson = $$"""
        {
          "status": "completed",
          "steps": [
            {
              "type": "thought",
              "signature": "{{signature}}"
            },
            {
              "type": "model_output",
              "content": [
                {
                  "type": "image",
                  "mime_type": "image/jpeg",
                  "data": "not-base64"
                }
              ]
            }
          ],
          "usage": {
            "total_input_tokens": 10,
            "total_output_tokens": 20,
            "total_tokens": 30
          }
        }
        """;
        GoogleStreamingResponseAnalyzer analyzer = CreateAnalyzer(
            maximumFilteredResponseBytes: 1024,
            maximumDiagnosticTextCharacters: 512);
        byte[] bytes = Encoding.UTF8.GetBytes(responseJson);

        for (int offset = 0; offset < bytes.Length; offset += 17)
        {
            int count = Math.Min(17, bytes.Length - offset);
            analyzer.Append(bytes.AsSpan(offset, count));
        }

        ProviderGenerationSummary summary = analyzer.Complete();

        summary.State.Should().Be("completed");
        summary.ResultCount.Should().Be(1);
        summary.ContentTypes.Should().ContainSingle()
            .Which.Should().Be(GenerationImageContentTypes.Jpeg);
        summary.Usage?.TotalTokens.Should().Be(30);
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

    private static GoogleStreamingResponseAnalyzer CreateAnalyzer(
        int maximumFilteredResponseBytes =
            MaximumFilteredResponseBytes,
        int maximumStructureDepth =
            MaximumStructureDepth,
        int maximumDiagnosticTextCharacters =
            MaximumDiagnosticTextCharacters)
    {
        return new GoogleStreamingResponseAnalyzer(
            new GoogleInteractionsResponseParser(),
            new GoogleInteractionsFailureClassifier(),
            maximumFilteredResponseBytes,
            maximumStructureDepth,
            maximumDiagnosticTextCharacters);
    }

    private static GoogleInteractionsOptions CreateOptions()
    {
        return new GoogleInteractionsOptions
        {
            Base64InputBufferSize = 48,
            Base64OutputBufferSize = 64,
            ServiceTier = "flex",
            StoreInteractions = true
        };
    }

    private static string CreateCompletedResponse(string data)
    {
        return $$"""
        {
          "status": "completed",
          "output": [
            {
              "type": "image",
              "mime_type": "image/jpeg",
              "data": "{{data}}"
            }
          ],
          "usage": {
            "total_input_tokens": 10,
            "total_output_tokens": 20,
            "total_tokens": 30
          }
        }
        """;
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
