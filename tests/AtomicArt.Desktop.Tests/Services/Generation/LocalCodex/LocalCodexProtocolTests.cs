using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;

namespace AtomicArt.Desktop.Tests.Services.Generation.LocalCodex;

public sealed class LocalCodexProtocolTests
{
    private const string TurnStart = """
        {"id":1,"result":{"thread":{"id":"thread-1"},"model":"gpt-6.2-astra"}}
        {"id":2,"result":{"turn":{"id":"turn-1"}}}

        """;

    [Fact]
    public async Task InitializeAsync_WithApiKeyAccount_RequiresChatGptSignIn()
    {
        using StringReader reader = new("""
            {"id":1,"result":{}}
            {"id":2,"result":{"account":{"type":"apiKey"}}}
            """);
        using StringWriter writer = new();
        LocalCodexProtocol protocol = new(reader, writer);

        Func<Task> act = () => protocol.InitializeAsync(CancellationToken.None);

        (await act.Should().ThrowAsync<GenerationAttemptException>()).Which.SafeErrorCode.Should()
            .Be(GenerationClientFailureCodes.CodexSignInRequired);
    }

    [Fact]
    public async Task GenerateAsync_WithReferenceAndMatchingImageEvent_ReturnsGeneratedImage()
    {
        using StringReader reader = new(TurnStart + """
            {"method":"item/completed","params":{"threadId":"thread-1","turnId":"other","item":{"type":"imageGeneration","status":"completed","result":"other"}}}
            {"method":"item/completed","params":{"threadId":"thread-1","turnId":"other","item":{"type":"agentMessage","text":"Different turn"}}}
            {"method":"item/completed","params":{"threadId":"thread-1","turnId":"turn-1","item":{"type":"imageGeneration","status":"completed","result":"actual"}}}
            {"method":"item/completed","params":{"threadId":"thread-1","turnId":"turn-1","item":{"type":"agentMessage","text":"Updated the reflection."}}}
            {"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed"}}}
            """);
        using StringWriter writer = new();
        LocalCodexProtocol protocol = new(reader, writer);
        const string ReferencePath = "reference.png";
        LocalCodexTurnOptions options = new("gpt-6.2-astra", "GPT-6.2 Astra", "high");

        (string threadId, string? modelId, JsonElement image, string text) = await protocol.GenerateAsync(
            Path.GetTempPath(), new object[] { new { type = "localImage", path = ReferencePath } }, options, CancellationToken.None);

        threadId.Should().Be("thread-1");
        modelId.Should().Be(options.ModelId);
        image.GetProperty("result").GetString().Should().Be("actual");
        text.Should().Be("Updated the reflection.");
        string[] requests = writer.ToString().Split(Environment.NewLine);
        using JsonDocument startRequest = JsonDocument.Parse(requests[0]);
        startRequest.RootElement.GetProperty("params").GetProperty("model").GetString().Should().Be(options.ModelId);
        using JsonDocument request = JsonDocument.Parse(requests[1]);
        request.RootElement.GetProperty("params").GetProperty("effort").GetString().Should().Be(options.ReasoningEffort);
        JsonElement attachment = request.RootElement.GetProperty("params").GetProperty("input")[0];
        attachment.GetProperty("type").GetString().Should().Be("localImage");
        attachment.GetProperty("path").GetString().Should().Be(ReferencePath);
    }

    [Fact]
    public async Task GenerateAsync_WithImageUsageLimit_ReturnsNonRetryableRateLimit()
    {
        using StringReader reader = new(TurnStart + """
            {"method":"item/completed","params":{"threadId":"thread-1","turnId":"turn-1","item":{"type":"imageGeneration","status":"failed","failure":{"type":"usageLimitExceeded"}}}}
            {"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed"}}}
            """);
        using StringWriter writer = new();
        LocalCodexProtocol protocol = new(reader, writer);

        Func<Task> act = () => protocol.GenerateAsync(Path.GetTempPath(), Array.Empty<object>(), null, CancellationToken.None);

        GenerationAttemptException failure = (await act.Should().ThrowAsync<GenerationAttemptException>()).Which;
        failure.SafeErrorCode.Should().Be(GenerationProviderFailureErrorCodes.RateLimited);
        failure.Retryable.Should().BeFalse();
    }

    [Fact]
    public async Task ListTextModelsAsync_WithPaginatedCatalog_ReturnsAllModelsAndConfiguredDefaults()
    {
        using StringReader reader = new("""
            {"id":1,"result":{"data":[{"model":"sol","displayName":"Sol","isDefault":true}],"nextCursor":"page-2"}}
            {"id":2,"result":{"data":[{"model":"astra","displayName":"Astra","supportedReasoningEfforts":[{"reasoningEffort":"high"}],"defaultReasoningEffort":"high"}],"nextCursor":null}}
            {"id":3,"result":{"config":{"model":"astra","model_reasoning_effort":"high"}}}
            """);
        using StringWriter writer = new();
        LocalCodexProtocol protocol = new(reader, writer);

        (IReadOnlyList<LocalCodexTextModel> models, string? defaultModel, string? defaultEffort) =
            await protocol.ListTextModelsAsync(CancellationToken.None);

        models.Select(model => model.Id).Should().Equal("sol", "astra");
        models[1].ReasoningEfforts.Should().Equal("high");
        defaultModel.Should().Be("astra");
        defaultEffort.Should().Be("high");
        using JsonDocument nextPageRequest = JsonDocument.Parse(writer.ToString().Split(Environment.NewLine)[1]);
        nextPageRequest.RootElement.GetProperty("params").GetProperty("cursor").GetString().Should().Be("page-2");
    }
}
