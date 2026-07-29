using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationFailureMessageResolverTests
{
    [Fact]
    public void GetUserMessage_WithKnownProviderCode_ReturnsSpecificMessage()
    {
        string message = GenerationFailureMessageResolver.GetUserMessage(
            GenerationProviderFailureErrorCodes.Authentication);

        message.Should().Be(UiStrings.GenerationAuthenticationFailed);
    }

    [Fact]
    public void GetUserMessage_WithUnknownCode_ReturnsGenericMessage()
    {
        string message = GenerationFailureMessageResolver.GetUserMessage(
            "UNKNOWN_ERROR_CODE");

        message.Should().Be(UiStrings.GenerationFailed);
    }

    [Fact]
    public void GetUserMessage_WithHttpRequestException_ReturnsServerUnavailableMessage()
    {
        HttpRequestException exception = new("Connection refused.");

        string message = GenerationFailureMessageResolver.GetUserMessage(exception);

        message.Should().Be(UiStrings.GenerationApiUnavailable);
    }

    [Fact]
    public void GetUserMessage_WithUnexpectedException_ReturnsGenericMessage()
    {
        InvalidOperationException exception = new("Unexpected failure.");

        string message = GenerationFailureMessageResolver.GetUserMessage(exception);

        message.Should().Be(UiStrings.GenerationFailed);
    }
}
