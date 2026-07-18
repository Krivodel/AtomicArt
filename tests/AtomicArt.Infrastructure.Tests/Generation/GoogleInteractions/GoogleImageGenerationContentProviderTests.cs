using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Common.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation.GoogleInteractions;

public sealed class GoogleImageGenerationContentProviderTests
{
    [Fact]
    public async Task GetContentAsync_WithCompletedResponse_ReturnsImageUsageAndDuration()
    {
        TestGoogleInteractionsClient client = new(CreateCompletedResponse());
        TestDateTimeProvider dateTimeProvider = new(
            new DateTime(2026, 7, 4, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 4, 10, 0, 30, DateTimeKind.Utc));
        GoogleImageGenerationContentProvider provider = new(
            new GoogleInteractionsRequestBuilder(),
            client,
            new GoogleInteractionsResponseParser(),
            new GenerationUsagePriceCalculator(),
            dateTimeProvider,
            NullLogger<GoogleImageGenerationContentProvider>.Instance);
        ImageGenerationContentProviderContext context = CreateContext("test-provider-key");

        ImageGenerationContentResult result = await provider.GetContentAsync(context, CancellationToken.None);

        result.ContentType.Should().Be("image/jpeg");
        result.Base64Data.Should().Be("/9j/4AAQSkZJRg==");
        result.CompletedAtUtc.Should().Be(new DateTime(2026, 7, 4, 10, 0, 30, DateTimeKind.Utc));
        result.GenerationDuration.Should().Be(TimeSpan.FromSeconds(30));
        result.Price.Should().BeEquivalentTo(new GenerationPriceDto(0.0678m, "USD", "ActualProviderUsage"));
        result.Usage.Should().BeEquivalentTo(new
        {
            TotalInputTokens = 1200,
            TotalOutputTokens = 1120,
            TotalTokens = 2320
        });
        client.ProviderCredential.Should().Be("test-provider-key");
        client.RequestJson.Should().NotContain("test-provider-key");
        client.RequestJson.Should().Contain(ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().ProviderModelId);
    }

    [Fact]
    public async Task GetContentAsync_WithFourKRequest_UsesResolutionImageTokensForPrice()
    {
        TestGoogleInteractionsClient client = new(CreateCompletedFourKResponse());
        TestDateTimeProvider dateTimeProvider = new(
            new DateTime(2026, 7, 4, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 4, 10, 0, 30, DateTimeKind.Utc));
        GoogleImageGenerationContentProvider provider = new(
            new GoogleInteractionsRequestBuilder(),
            client,
            new GoogleInteractionsResponseParser(),
            new GenerationUsagePriceCalculator(),
            dateTimeProvider,
            NullLogger<GoogleImageGenerationContentProvider>.Instance);
        ImageGenerationContentProviderContext context = CreateContext(
            "test-provider-key",
            "4K");

        ImageGenerationContentResult result = await provider.GetContentAsync(context, CancellationToken.None);

        result.Price.Should().BeEquivalentTo(new GenerationPriceDto(0.151845m, "USD", "ActualProviderUsage"));
        result.Price?.Amount.Should().NotBe(0.0672m);
        result.Usage.Should().BeEquivalentTo(new
        {
            TotalInputTokens = 1200,
            TotalOutputTokens = 1120,
            TotalThoughtTokens = 5,
            TotalTokens = 2335
        });
    }

    [Fact]
    public async Task GetContentAsync_WithoutProviderCredential_DoesNotCallClient()
    {
        TestGoogleInteractionsClient client = new(CreateCompletedResponse());
        GoogleImageGenerationContentProvider provider = new(
            new GoogleInteractionsRequestBuilder(),
            client,
            new GoogleInteractionsResponseParser(),
            new GenerationUsagePriceCalculator(),
            new TestDateTimeProvider(new DateTime(2026, 7, 4, 10, 0, 0, DateTimeKind.Utc)),
            NullLogger<GoogleImageGenerationContentProvider>.Instance);
        ImageGenerationContentProviderContext context = CreateContext(null);

        Func<Task> act = () => provider.GetContentAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The temporary provider credential was not supplied.");
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task GetContentAsync_WithTextOnlyResponse_LogsNoImageDiagnosticsAndDoesNotRetry()
    {
        TestGoogleInteractionsClient client = new(CreateTextOnlyResponse());
        RecordingProviderLogger logger = new();
        GoogleImageGenerationContentProvider provider = new(
            new GoogleInteractionsRequestBuilder(),
            client,
            new GoogleInteractionsResponseParser(),
            new GenerationUsagePriceCalculator(),
            new TestDateTimeProvider(new DateTime(2026, 7, 4, 10, 0, 0, DateTimeKind.Utc)),
            logger);
        ImageGenerationContentProviderContext context = CreateContext("test-provider-key");

        Func<Task> act = () => provider.GetContentAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<GoogleInteractionsException>()
            .WithMessage("The generation provider did not return a JPEG image.");
        client.Calls.Should().Be(1);
        RecordingProviderLoggerEntry entry = logger.Entries.Should().ContainSingle().Which;
        entry.LogLevel.Should().Be(LogLevel.Warning);
        entry.Message.Should().Contain("Category text_only");
        entry.Message.Should().Contain("Status completed");
        entry.Message.Should().Contain("HasOutput True");
        entry.Message.Should().Contain("HasStepsTextContent True");
        entry.Message.Should().Contain("TextContentLength 4");
        entry.Message.Should().NotContain("done");
        entry.Message.Should().NotContain("test-provider-key");
    }

    private static ImageGenerationContentProviderContext CreateContext(
        string? providerCredential,
        string resolution = "1K")
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        ImageGenerationRequestDto request = new(
            metadata.Id,
            "Prompt",
            "1:1",
            resolution,
            metadata.Temperature.Default,
            1,
            []);

        return new ImageGenerationContentProviderContext(
            request,
            GenerationProviderIds.Google,
            metadata.ProviderModelId,
            CreatePricing(),
            0,
            providerCredential);
    }

    private static GenerationModelPricingMetadataDto CreatePricing()
    {
        return new GenerationModelPricingMetadataDto(
            "USD",
            0.50m,
            3.00m,
            60.00m,
            1120,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["1K"] = 1120,
                ["4K"] = 2520
            });
    }

    private static string CreateCompletedResponse()
    {
        return """
            {
              "status": "completed",
              "steps": [
                {
                  "content": [
                    {
                      "type": "image",
                      "mime_type": "image/jpeg",
                      "data": "/9j/4AAQSkZJRg=="
                    }
                  ]
                }
              ],
              "usage": {
                "total_input_tokens": 1200,
                "total_output_tokens": 1120,
                "total_tokens": 2320
              }
            }
            """;
    }

    private static string CreateCompletedFourKResponse()
    {
        return """
            {
              "status": "completed",
              "steps": [
                {
                  "content": [
                    {
                      "type": "image",
                      "mime_type": "image/jpeg",
                      "data": "/9j/4AAQSkZJRg=="
                    }
                  ]
                }
              ],
              "usage": {
                "total_input_tokens": 1200,
                "total_output_tokens": 1120,
                "total_thought_tokens": 5,
                "total_tokens": 2335,
                "output_tokens_by_modality": [
                  {
                    "modality": "image",
                    "tokens": 1120
                  },
                  {
                    "modality": "text",
                    "tokens": 10
                  }
                ]
              }
            }
            """;
    }

    private static string CreateTextOnlyResponse()
    {
        return """
            {
              "status": "completed",
              "output": [],
              "steps": [
                {
                  "content": [
                    {
                      "type": "text",
                      "text": "done"
                    }
                  ]
                }
              ],
              "usage": {
                "total_input_tokens": 1200,
                "total_output_tokens": 12,
                "total_tokens": 1212
              }
            }
            """;
    }

    private sealed class TestGoogleInteractionsClient : IGoogleInteractionsClient
    {
        private readonly string _responseJson;

        public int Calls { get; private set; }
        public string RequestJson { get; private set; } = string.Empty;
        public string ProviderCredential { get; private set; } = string.Empty;

        public TestGoogleInteractionsClient(string responseJson)
        {
            _responseJson = responseJson;
        }

        public Task<string> CreateInteractionAsync(
            string requestJson,
            string providerCredential,
            CancellationToken ct)
        {
            Calls++;
            RequestJson = requestJson;
            ProviderCredential = providerCredential;

            return Task.FromResult(_responseJson);
        }
    }

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        private readonly Queue<DateTime> _values;

        public DateTime UtcNow => _values.Count > 0
            ? _values.Dequeue()
            : throw new InvalidOperationException("Test clock is exhausted.");

        public TestDateTimeProvider(params DateTime[] values)
        {
            _values = new Queue<DateTime>(values);
        }
    }

    private sealed record RecordingProviderLoggerEntry(
        LogLevel LogLevel,
        string Message);

    private sealed class RecordingProviderLogger : ILogger<GoogleImageGenerationContentProvider>
    {
        private readonly List<RecordingProviderLoggerEntry> _entries = [];

        public IReadOnlyList<RecordingProviderLoggerEntry> Entries => _entries;

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            _entries.Add(new RecordingProviderLoggerEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        public static NullDisposable Instance { get; } = new();

        private NullDisposable()
        {
        }

        public void Dispose()
        {
        }
    }
}
