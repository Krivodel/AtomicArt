using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Application.Tests.Features.Generation.Models;

public sealed class ImageGenerationProviderFailureCatalogTests
{
    [Theory]
    [InlineData(ImageGenerationProviderFailureKind.Authentication, "ERR-GEN-005")]
    [InlineData(ImageGenerationProviderFailureKind.Authorization, "ERR-GEN-006")]
    [InlineData(ImageGenerationProviderFailureKind.RateLimited, "ERR-GEN-007")]
    [InlineData(ImageGenerationProviderFailureKind.InvalidResponse, "ERR-GEN-008")]
    [InlineData(ImageGenerationProviderFailureKind.Timeout, "ERR-GEN-009")]
    [InlineData(ImageGenerationProviderFailureKind.Unavailable, "ERR-GEN-010")]
    [InlineData(ImageGenerationProviderFailureKind.RequestRejected, "ERR-GEN-011")]
    [InlineData(ImageGenerationProviderFailureKind.ResourceNotFound, "ERR-GEN-012")]
    [InlineData(ImageGenerationProviderFailureKind.InternalError, "ERR-GEN-013")]
    [InlineData(ImageGenerationProviderFailureKind.Unknown, "ERR-GEN-014")]
    public void GetErrorCode_WithKnownFailureKind_ReturnsMappedCode(
        ImageGenerationProviderFailureKind failureKind,
        string expectedErrorCode)
    {
        string errorCode = ImageGenerationProviderFailureCatalog.GetErrorCode(failureKind);

        errorCode.Should().Be(expectedErrorCode);
    }

    [Theory]
    [InlineData("ERR-GEN-005", ImageGenerationProviderFailureKind.Authentication)]
    [InlineData("ERR-GEN-006", ImageGenerationProviderFailureKind.Authorization)]
    [InlineData("ERR-GEN-007", ImageGenerationProviderFailureKind.RateLimited)]
    [InlineData("ERR-GEN-008", ImageGenerationProviderFailureKind.InvalidResponse)]
    [InlineData("ERR-GEN-009", ImageGenerationProviderFailureKind.Timeout)]
    [InlineData("ERR-GEN-010", ImageGenerationProviderFailureKind.Unavailable)]
    [InlineData("ERR-GEN-011", ImageGenerationProviderFailureKind.RequestRejected)]
    [InlineData("ERR-GEN-012", ImageGenerationProviderFailureKind.ResourceNotFound)]
    [InlineData("ERR-GEN-013", ImageGenerationProviderFailureKind.InternalError)]
    [InlineData("ERR-GEN-014", ImageGenerationProviderFailureKind.Unknown)]
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

        errorCode.Should().Be("ERR-GEN-014");
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
