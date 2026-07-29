using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationFailureCodeResolverTests
{
    [Fact]
    public void GetFailureCode_WithProviderFailure_ReturnsSafeProviderCode()
    {
        GenerationAttemptException exception = new(
            "Provider rejected the request.",
            GenerationProviderFailureErrorCodes.RequestRejected,
            retryable: false);

        string failureCode = GenerationFailureCodeResolver.GetFailureCode(exception);

        failureCode.Should().Be(GenerationProviderFailureErrorCodes.RequestRejected);
    }

    [Fact]
    public void GetFailureCode_WithHttpRequestException_ReturnsApiUnavailableCode()
    {
        HttpRequestException exception = new("Connection refused.");

        string failureCode = GenerationFailureCodeResolver.GetFailureCode(exception);

        failureCode.Should().Be(GenerationClientFailureCodes.ApiUnavailable);
    }

    [Fact]
    public void GetFailureCode_WithUnexpectedException_ReturnsUnknownCode()
    {
        InvalidOperationException exception = new("Unexpected failure.");

        string failureCode = GenerationFailureCodeResolver.GetFailureCode(exception);

        failureCode.Should().Be(GenerationClientFailureCodes.Unknown);
    }
}
