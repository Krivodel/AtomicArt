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
            {"method":"item/completed","params":{"threadId":"thread-1","turnId":"turn-1","item":{"type":"imageGeneration","status":"completed","result":"actual","revisedPrompt":"Warm lake light at dusk.\nKeep the boat.","transparentBackground":false,"generationId":"g1"}}}
            {"method":"item/completed","params":{"threadId":"thread-1","turnId":"turn-1","item":{"type":"agentMessage","text":"Updated the reflection."}}}
            {"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed"}}}
            """);
        using StringWriter writer = new();
        LocalCodexProtocol protocol = new(reader, writer);
        const string ReferencePath = "reference.png";
        LocalCodexTurnOptions options = new("gpt-6.2-astra", "GPT-6.2 Astra", "high");

        LocalCodexTurnResult reply = await protocol.GenerateAsync(
            Path.GetTempPath(), new object[] { new { type = "localImage", path = ReferencePath } }, options, CancellationToken.None);

        reply.ThreadId.Should().Be("thread-1");
        reply.ModelId.Should().Be(options.ModelId);
        reply.Image?.GetProperty("result").GetString().Should().Be("actual");
        reply.Text.Should().Be("Updated the reflection.");
        reply.GetImageCall("hash").Should().Be(new GenerationChatImageCall("hash",
            "Warm lake light at dusk.\nKeep the boat.", false, "g1"));
        string[] requests = writer.ToString().Split(Environment.NewLine);
        using JsonDocument startRequest = JsonDocument.Parse(requests[0]);
        startRequest.RootElement.GetProperty("params").GetProperty("model").GetString().Should().Be(options.ModelId);
        using JsonDocument request = JsonDocument.Parse(requests[1]);
        request.RootElement.GetProperty("params").GetProperty("effort").GetString().Should().Be(options.ReasoningEffort);
        JsonElement attachment = request.RootElement.GetProperty("params").GetProperty("input")[0];
        attachment.GetProperty("type").GetString().Should().Be("localImage");
        attachment.GetProperty("path").GetString().Should().Be(ReferencePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \r\n\t")]
    [InlineData("Always reply in Russian.")]
    [InlineData("  Reply with text only.\n\nKeep these spaces.  \n")]
    [InlineData("<style>Тёплые цвета & мягкий свет.</style>\n")]
    public async Task GenerateAsync_WithCustomSystemPrompt_PreservesDefaultAndAddsExplicitUserPriority(string? customPrompt)
    {
        const string DefaultInstructions = """
            By default, create or edit an image, even if the description is short or ambiguous. Use the built-in image generator once per request. Use the supplied references and conversation context.

            Reply with text alone only when explicitly asked for information, explanations, recommendations, prompts, or a text response. Use plain text without Markdown. Quote recorded image prompts verbatim. If the exact prompt is unavailable, say so explicitly. Say if the image generator is unavailable. Do not run commands, use external tools, or modify files.
            """;
        const string UserPriority = "Instructions in <user_instructions> take priority over instructions in <default_instructions> when they conflict.";
        using StringReader reader = new(TurnStart + CreateTextEvent("Done", "final_answer") + "\n" + """
            {"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed"}}}
            """);
        using StringWriter writer = new();
        LocalCodexProtocol protocol = new(reader, writer, customPrompt);

        await protocol.GenerateAsync(Path.GetTempPath(), Array.Empty<object>(), null, CancellationToken.None);

        using JsonDocument start = JsonDocument.Parse(writer.ToString().Split(Environment.NewLine)[0]);
        string expected = string.IsNullOrWhiteSpace(customPrompt) ? DefaultInstructions : $"""
            {UserPriority}

            <default_instructions>
            {DefaultInstructions}
            </default_instructions>

            <user_instructions>
            {customPrompt}
            </user_instructions>
            """;
        start.RootElement.GetProperty("params").GetProperty("developerInstructions").GetString().Should().Be(expected);
    }

    [Theory]
    [InlineData("commentary")]
    [InlineData("final_answer")]
    public async Task GenerateAsync_WithPromptInAssistantText_PreservesItWithoutInventingAnImageCall(string phase)
    {
        using StringReader reader = new(TurnStart + $$$$"""
            {"method":"item/completed","params":{"threadId":"thread-1","turnId":"turn-1","item":{"type":"agentMessage","phase":"{{{{phase}}}}","text":"Image prompt:\nТихая бухта.\nТёплый свет."}}}
            {"method":"item/completed","params":{"threadId":"thread-1","turnId":"turn-1","item":{"type":"agentMessage","phase":"final_answer","text":"Here is the recorded prompt."}}}
            {"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed"}}}
            """);
        using StringWriter writer = new();
        LocalCodexProtocol protocol = new(reader, writer);

        LocalCodexTurnResult reply = await protocol.GenerateAsync(Path.GetTempPath(), Array.Empty<object>(), null, CancellationToken.None);

        reply.Text.Should().Be("Image prompt:\nТихая бухта.\nТёплый свет.Here is the recorded prompt.");
        reply.Image.Should().BeNull();
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("failed")]
    public async Task GenerateAsync_WithWhitespaceAcrossTextItems_PreservesEveryCharacter(string status)
    {
        string[] texts = ["          First\r\n", "\n\n\n\n", "Second          line\n\n\n\n\n"];
        string events = string.Join("\n", texts.Select(text => CreateTextEvent(text, "final_answer")));
        string completed = JsonSerializer.Serialize(new
        {
            method = "turn/completed",
            @params = new { threadId = "thread-1", turn = new { id = "turn-1", status } }
        });
        using StringReader reader = new(TurnStart + events + "\n" + completed);
        using StringWriter writer = new();
        LocalCodexProtocol protocol = new(reader, writer);
        const string Expected = "          First\r\n\n\n\n\nSecond          line\n\n\n\n\n";

        if (status == "completed")
        {
            LocalCodexTurnResult reply = await protocol.GenerateAsync(
                Path.GetTempPath(), Array.Empty<object>(), null, CancellationToken.None);

            reply.Text.Should().Be(Expected);
        }
        else
        {
            Func<Task> generate = () => protocol.GenerateAsync(
                Path.GetTempPath(), Array.Empty<object>(), null, CancellationToken.None);

            GenerationAttemptException failure = (await generate.Should().ThrowAsync<GenerationAttemptException>()).Which;
            failure.ChatResult?.Text.Should().Be(Expected);
        }
    }

    [Fact]
    public async Task GenerateAsync_WithWhitespaceOnlyFinalAnswer_PreservesIt()
    {
        const string Text = "          \n\n\n\n\n";
        using StringReader reader = new(TurnStart + CreateTextEvent(Text, "final_answer") + "\n" + """
            {"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed"}}}
            """);
        using StringWriter writer = new();
        LocalCodexProtocol protocol = new(reader, writer);

        LocalCodexTurnResult reply = await protocol.GenerateAsync(
            Path.GetTempPath(), Array.Empty<object>(), null, CancellationToken.None);

        reply.Text.Should().Be(Text);
        reply.Image.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Тихая бухта.\nТёплый свет.")]
    public async Task GenerateAsync_WithOptionalToolPrompt_RecordsOnlyTheReportedValue(string? prompt)
    {
        string imageEvent = JsonSerializer.Serialize(new
        {
            method = "item/completed",
            @params = new
            {
                threadId = "thread-1",
                turnId = "turn-1",
                item = new { type = "imageGeneration", status = "completed", revisedPrompt = prompt, result = "image" }
            }
        });
        using StringReader reader = new(TurnStart + imageEvent + "\n" + """
            {"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed"}}}
            """);
        using StringWriter writer = new();
        LocalCodexProtocol protocol = new(reader, writer);

        LocalCodexTurnResult reply = await protocol.GenerateAsync(Path.GetTempPath(), Array.Empty<object>(), null, CancellationToken.None);

        reply.GetImageCall("hash").RevisedPrompt.Should().Be(string.IsNullOrEmpty(prompt) ? null : prompt);
        reply.Text.Should().BeEmpty();
    }

    [Theory]
    [InlineData("completed", "commentary", "Working on it.")]
    [InlineData("completed", "final_answer", "")]
    [InlineData("failed", "final_answer", "Partial response")]
    public async Task GenerateAsync_WithoutACompletedAnswer_ReportsFailure(string status, string phase, string text)
    {
        using StringReader reader = new(TurnStart + $$$$"""
            {"method":"item/completed","params":{"threadId":"thread-1","turnId":"turn-1","item":{"type":"agentMessage","phase":"{{{{phase}}}}","text":"{{{{text}}}}"}}}
            {"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"{{{{status}}}}"}}}
            """);
        using StringWriter writer = new();
        LocalCodexProtocol protocol = new(reader, writer);

        Func<Task> generate = () => protocol.GenerateAsync(Path.GetTempPath(), Array.Empty<object>(), null, CancellationToken.None);

        (await generate.Should().ThrowAsync<GenerationAttemptException>()).Which.SafeErrorCode.Should().Be(GenerationClientFailureCodes.CodexNoImage);
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
    public async Task GenerateAsync_WithFailedImageAndAssistantReply_PreservesTheReplyOnFailure()
    {
        using StringReader reader = new(TurnStart + """
            {"method":"item/completed","params":{"threadId":"thread-1","turnId":"turn-1","item":{"type":"imageGeneration","status":"failed","failure":null,"result":""}}}
            {"method":"item/completed","params":{"threadId":"thread-1","turnId":"turn-1","item":{"type":"agentMessage","text":"The image generator rejected the request."}}}
            {"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"completed"}}}
            """);
        using StringWriter writer = new();
        LocalCodexProtocol protocol = new(reader, writer);

        Func<Task> generate = () => protocol.GenerateAsync(Path.GetTempPath(), Array.Empty<object>(), null, CancellationToken.None);

        GenerationAttemptException failure = (await generate.Should().ThrowAsync<GenerationAttemptException>()).Which;
        failure.SafeErrorCode.Should().Be(GenerationClientFailureCodes.CodexNoImage);
        failure.Retryable.Should().BeFalse();
        failure.ChatResult.Should().Be(new GenerationChatResult("The image generator rejected the request.", null));
        failure.Message.Should().NotContain(failure.ChatResult?.Text);
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

    private static string CreateTextEvent(string text, string phase)
    {
        return JsonSerializer.Serialize(new
        {
            method = "item/completed",
            @params = new
            {
                threadId = "thread-1",
                turnId = "turn-1",
                item = new { type = "agentMessage", phase, text }
            }
        });
    }
}
