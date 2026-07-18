using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class DesktopTypeDiscoveryTests
{
    [Fact]
    public void FindPublicImplementations_WithMultipleMarkers_ReturnsDistinctOrderedTypes()
    {
        IReadOnlyList<Type> implementationTypes =
            DesktopTypeDiscovery.FindPublicImplementations(
                typeof(ISettingsDefinition),
                typeof(IUiScaleOptionDefinition));

        implementationTypes.Should().OnlyHaveUniqueItems();
        implementationTypes.Should().Equal(
            implementationTypes.OrderBy(type => type.FullName, StringComparer.Ordinal));
        implementationTypes.Should().Contain(typeof(ApiBaseAddressSettingDefinition));
        implementationTypes.Should().Contain(typeof(UiScale100OptionDefinition));
    }

    [Fact]
    public void FindPublicImplementations_WithInternalImplementations_ReturnsEmptyList()
    {
        IReadOnlyList<Type> implementationTypes =
            DesktopTypeDiscovery.FindPublicImplementations(typeof(IGenerationImageFormat));

        implementationTypes.Should().BeEmpty();
    }

    [Fact]
    public void FindAllImplementations_WithInternalImplementations_ReturnsOrderedTypes()
    {
        IReadOnlyList<Type> implementationTypes =
            DesktopTypeDiscovery.FindAllImplementations(typeof(IGenerationImageFormat));

        implementationTypes.Should().Equal(
            typeof(JpegGenerationImageFormat),
            typeof(PngGenerationImageFormat),
            typeof(WebpGenerationImageFormat));
    }
}
