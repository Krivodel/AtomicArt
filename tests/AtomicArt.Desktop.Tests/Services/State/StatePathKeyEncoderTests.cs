using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Tests.Services.State;

public sealed class StatePathKeyEncoderTests
{
    [Fact]
    public void Encode_WithUnsafePanelId_ReturnsSafePathSegment()
    {
        StatePathKeyEncoder encoder = new();
        string panelId = @"..\unsafe/panel:id";

        string encoded = encoder.Encode(panelId);

        encoded.Should().HaveLength(64);
        encoded.Should().MatchRegex("^[0-9a-f]{64}$");
        encoded.Should().NotContain("..");
        encoded.Should().NotContain("\\");
        encoded.Should().NotContain("/");
        encoded.Should().NotContain("unsafe");
    }

    [Fact]
    public void Encode_WithSameValue_ReturnsSameSegment()
    {
        StatePathKeyEncoder encoder = new();
        string panelId = "nano-banana";

        string first = encoder.Encode(panelId);
        string second = encoder.Encode(panelId);

        first.Should().Be(second);
    }

    [Fact]
    public void Encode_WithEmptyValue_ThrowsArgumentException()
    {
        StatePathKeyEncoder encoder = new();

        Action act = () => encoder.Encode(string.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
