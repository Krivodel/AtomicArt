using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Dlss5;

namespace AtomicArt.Desktop.Tests.Services.Dlss5;

public sealed class Dlss5SessionStateSectionTests
{
    [Theory]
    [InlineData("{}", Dlss5SessionState.DefaultParameterAreaHeight)]
    [InlineData("{\"ParameterAreaHeight\":0}", Dlss5SessionState.MinimumParameterAreaHeight)]
    [InlineData("{\"ParameterAreaHeight\":10000}", Dlss5SessionState.MaximumParameterAreaHeight)]
    public void DeserializePayload_NormalizesParameterAreaHeight(
        string json,
        double expectedHeight)
    {
        Dlss5SessionStateSection section = new();
        using JsonDocument document = JsonDocument.Parse(json);

        Dlss5SessionState state = section.DeserializePayload(
                section.SchemaVersion,
                document.RootElement,
                new JsonSerializerOptions())
            .Should()
            .BeOfType<Dlss5SessionState>()
            .Subject;

        state.ParameterAreaHeight.Should().Be(expectedHeight);
    }
}
