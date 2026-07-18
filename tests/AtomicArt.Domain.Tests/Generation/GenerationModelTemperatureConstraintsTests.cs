using FluentAssertions;
using Xunit;

using AtomicArt.Domain.Exceptions;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Domain.Tests.Generation;

public sealed class GenerationModelTemperatureConstraintsTests
{
    [Theory]
    [InlineData(0.1d)]
    [InlineData(1d)]
    [InlineData(2d)]
    public void IsSupported_WithCatalogTemperature_ReturnsTrue(double temperature)
    {
        GenerationModelTemperatureConstraints constraints = CreateConstraints();

        bool isSupported = constraints.IsSupported(temperature);

        isSupported.Should().BeTrue();
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(0.15d)]
    [InlineData(2.1d)]
    public void IsSupported_WithUnsupportedTemperature_ReturnsFalse(double temperature)
    {
        GenerationModelTemperatureConstraints constraints = CreateConstraints();

        bool isSupported = constraints.IsSupported(temperature);

        isSupported.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithDefaultOutsideStep_ThrowsDomainException()
    {
        Action action = () => new GenerationModelTemperatureConstraints(0.1d, 2d, 0.15d, 0.1d);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("ERR-GEN-105");
    }

    [Fact]
    public void Constructor_WithMaximumOutsideStep_ThrowsDomainException()
    {
        Action action = () => new GenerationModelTemperatureConstraints(0.1d, 2d, 1d, 0.3d);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("ERR-GEN-106");
    }

    private static GenerationModelTemperatureConstraints CreateConstraints()
    {
        return new GenerationModelTemperatureConstraints(0.1d, 2d, 1d, 0.1d);
    }
}
