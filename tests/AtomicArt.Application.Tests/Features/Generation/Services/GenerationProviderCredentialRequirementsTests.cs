using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Tests.Features.Generation.Services;

public sealed class GenerationProviderCredentialRequirementsTests
{
    [Fact]
    public void Resolve_WithGoogleProvider_RequiresCredentialAtBothBoundaries()
    {
        GenerationProviderCredentialRequirement requirement =
            GenerationProviderCredentialRequirements.Resolve(GenerationProviderIds.Google);

        requirement.RequiredAtApiBoundary.Should().BeTrue();
        requirement.RequiredForApplicationValidation.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WithTestProvider_DoesNotRequireCredential()
    {
        GenerationProviderCredentialRequirement requirement =
            GenerationProviderCredentialRequirements.Resolve(GenerationProviderIds.Test);

        requirement.RequiredAtApiBoundary.Should().BeFalse();
        requirement.RequiredForApplicationValidation.Should().BeFalse();
    }

    [Fact]
    public void Resolve_WithOtherProvider_PreservesBoundarySpecificRequirements()
    {
        GenerationProviderCredentialRequirement requirement =
            GenerationProviderCredentialRequirements.Resolve("other");

        requirement.RequiredAtApiBoundary.Should().BeTrue();
        requirement.RequiredForApplicationValidation.Should().BeFalse();
    }
}
