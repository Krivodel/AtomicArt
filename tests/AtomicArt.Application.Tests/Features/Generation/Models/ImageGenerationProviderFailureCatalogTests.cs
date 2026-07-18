using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Tests.Features.Generation.Models;

public sealed class ImageGenerationProviderFailureCatalogTests
{
    [Theory]
    [InlineData(
        ImageGenerationProviderFailureKind.Authentication,
        GenerationProviderFailureErrorCodes.Authentication)]
    [InlineData(
        ImageGenerationProviderFailureKind.Authorization,
        GenerationProviderFailureErrorCodes.Authorization)]
    [InlineData(
        ImageGenerationProviderFailureKind.RateLimited,
        GenerationProviderFailureErrorCodes.RateLimited)]
    [InlineData(
        ImageGenerationProviderFailureKind.InvalidResponse,
        GenerationProviderFailureErrorCodes.InvalidResponse)]
    [InlineData(
        ImageGenerationProviderFailureKind.Timeout,
        GenerationProviderFailureErrorCodes.Timeout)]
    [InlineData(
        ImageGenerationProviderFailureKind.Unavailable,
        GenerationProviderFailureErrorCodes.Unavailable)]
    [InlineData(
        ImageGenerationProviderFailureKind.RequestRejected,
        GenerationProviderFailureErrorCodes.RequestRejected)]
    [InlineData(
        ImageGenerationProviderFailureKind.ResourceNotFound,
        GenerationProviderFailureErrorCodes.ResourceNotFound)]
    [InlineData(
        ImageGenerationProviderFailureKind.InternalError,
        GenerationProviderFailureErrorCodes.InternalError)]
    [InlineData(
        ImageGenerationProviderFailureKind.Unknown,
        GenerationProviderFailureErrorCodes.Unknown)]
    public void GetErrorCode_WithKnownFailureKind_ReturnsMappedCode(
        ImageGenerationProviderFailureKind failureKind,
        string expectedErrorCode)
    {
        string errorCode = ImageGenerationProviderFailureCatalog.GetErrorCode(failureKind);

        errorCode.Should().Be(expectedErrorCode);
    }

    [Theory]
    [InlineData(
        GenerationProviderFailureErrorCodes.Authentication,
        ImageGenerationProviderFailureKind.Authentication)]
    [InlineData(
        GenerationProviderFailureErrorCodes.Authorization,
        ImageGenerationProviderFailureKind.Authorization)]
    [InlineData(
        GenerationProviderFailureErrorCodes.RateLimited,
        ImageGenerationProviderFailureKind.RateLimited)]
    [InlineData(
        GenerationProviderFailureErrorCodes.InvalidResponse,
        ImageGenerationProviderFailureKind.InvalidResponse)]
    [InlineData(
        GenerationProviderFailureErrorCodes.Timeout,
        ImageGenerationProviderFailureKind.Timeout)]
    [InlineData(
        GenerationProviderFailureErrorCodes.Unavailable,
        ImageGenerationProviderFailureKind.Unavailable)]
    [InlineData(
        GenerationProviderFailureErrorCodes.RequestRejected,
        ImageGenerationProviderFailureKind.RequestRejected)]
    [InlineData(
        GenerationProviderFailureErrorCodes.ResourceNotFound,
        ImageGenerationProviderFailureKind.ResourceNotFound)]
    [InlineData(
        GenerationProviderFailureErrorCodes.InternalError,
        ImageGenerationProviderFailureKind.InternalError)]
    [InlineData(
        GenerationProviderFailureErrorCodes.Unknown,
        ImageGenerationProviderFailureKind.Unknown)]
    public void TryGetFailureKind_WithKnownErrorCode_ReturnsMappedFailureKind(
        string errorCode,
        ImageGenerationProviderFailureKind expectedFailureKind)
    {
        bool result = ImageGenerationProviderFailureCatalog.TryGetFailureKind(
            errorCode,
            out ImageGenerationProviderFailureKind failureKind);

        result.Should().BeTrue();
        failureKind.Should().Be(expectedFailureKind);
    }

    [Fact]
    public void GetErrorCode_WithUnknownFailureKind_ReturnsUnknownCode()
    {
        ImageGenerationProviderFailureKind failureKind =
            (ImageGenerationProviderFailureKind)int.MaxValue;

        string errorCode = ImageGenerationProviderFailureCatalog.GetErrorCode(failureKind);

        errorCode.Should().Be(GenerationProviderFailureErrorCodes.Unknown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ERR-GEN-999")]
    public void TryGetFailureKind_WithUnknownErrorCode_ReturnsFalse(string? errorCode)
    {
        bool result = ImageGenerationProviderFailureCatalog.TryGetFailureKind(
            errorCode,
            out ImageGenerationProviderFailureKind failureKind);

        result.Should().BeFalse();
        failureKind.Should().Be(default);
    }
}
