using FluentAssertions;
using Xunit;

using AtomicArt.Infrastructure.Generation.OpenRouter;

namespace AtomicArt.Infrastructure.Tests.Generation.OpenRouter;

public sealed class OpenRouterFlexModelPolicyTests
{
    [Fact]
    public void GetChatCompletionProviderTag_WithNanoBananaProPreview_UsesGoogleAiStudioFlex()
    {
        string? providerTag = OpenRouterFlexModelPolicy.GetChatCompletionProviderTag(
            "google/gemini-3-pro-image-preview");

        providerTag.Should().Be(OpenRouterFlexModelPolicy.GoogleAiStudioFlexProviderTag);
    }

    [Theory]
    [InlineData("google/gemini-3.1-flash-image")]
    [InlineData("google/gemini-3.1-flash-lite-image")]
    [InlineData("openai/gpt-image-2")]
    public void GetChatCompletionProviderTag_WithoutPublishedFlexEndpoint_DoesNotPinProvider(
        string providerModelId)
    {
        string? providerTag = OpenRouterFlexModelPolicy.GetChatCompletionProviderTag(providerModelId);

        providerTag.Should().BeNull();
    }
}
