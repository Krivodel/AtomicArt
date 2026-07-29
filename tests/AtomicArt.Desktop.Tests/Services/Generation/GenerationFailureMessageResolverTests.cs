using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationFailureMessageResolverTests
{
    [Fact]
    public void GetLocalizationKey_WithKnownProviderCode_ReturnsSpecificKey()
    {
        string localizationKey = GenerationFailureMessageResolver.GetLocalizationKey(
            GenerationProviderFailureErrorCodes.Authentication);

        localizationKey.Should().Be(
            GenerationUiLocalizationKeys.Errors.AuthenticationFailed);
    }

    [Fact]
    public void GetLocalizationKey_WithKnownProtocolCode_ReturnsSpecificKey()
    {
        string localizationKey = GenerationFailureMessageResolver.GetLocalizationKey(
            GenerationProtocolErrorCodes.ModelNotFound);

        localizationKey.Should().Be(GenerationUiLocalizationKeys.Errors.ModelNotFound);
    }

    [Fact]
    public void GetLocalizationKey_WithUnknownCode_ReturnsGenericKey()
    {
        string localizationKey = GenerationFailureMessageResolver.GetLocalizationKey(
            "UNKNOWN_ERROR_CODE");

        localizationKey.Should().Be(GenerationUiLocalizationKeys.Errors.Failed);
    }

    [Fact]
    public void GetLocalizationKey_WithHttpRequestException_ReturnsServerUnavailableKey()
    {
        HttpRequestException exception = new("Connection refused.");

        string localizationKey =
            GenerationFailureMessageResolver.GetLocalizationKey(exception);

        localizationKey.Should().Be(GenerationUiLocalizationKeys.Errors.ApiUnavailable);
    }

    [Fact]
    public void GetLocalizationKey_WithUnexpectedException_ReturnsGenericKey()
    {
        InvalidOperationException exception = new("Unexpected failure.");

        string localizationKey =
            GenerationFailureMessageResolver.GetLocalizationKey(exception);

        localizationKey.Should().Be(GenerationUiLocalizationKeys.Errors.Failed);
    }
}
