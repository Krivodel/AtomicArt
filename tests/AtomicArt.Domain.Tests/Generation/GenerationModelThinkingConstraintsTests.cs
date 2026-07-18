using FluentAssertions;
using Xunit;

using AtomicArt.Domain.Exceptions;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Domain.Tests.Generation;

public sealed class GenerationModelThinkingConstraintsTests
{
    [Fact]
    public void Constructor_WithSupportedLevels_CreatesSnapshot()
    {
        List<string> levels = ["minimal", "high"];

        GenerationModelThinkingConstraints constraints = new(levels, "minimal");
        levels[0] = "changed";

        constraints.Levels.Should().Equal("minimal", "high");
        constraints.Default.Should().Be("minimal");
        constraints.IsSupported("high").Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithUnsupportedDefault_ThrowsDomainException()
    {
        Action action = () => new GenerationModelThinkingConstraints(
            ["minimal", "high"],
            "medium");

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("ERR-GEN-111");
    }

    [Fact]
    public void Constructor_WithDuplicateLevels_ThrowsDomainException()
    {
        Action action = () => new GenerationModelThinkingConstraints(
            ["minimal", "minimal"],
            "minimal");

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("ERR-GEN-111");
    }
}
