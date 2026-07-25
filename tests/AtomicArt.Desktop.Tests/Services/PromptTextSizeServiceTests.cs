using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class PromptTextSizeServiceTests
{
    [Fact]
    public void Constructor_WithRegisteredDefinition_UsesDefinitionDefault()
    {
        PromptTextSizeSettingDefinition definition = new();

        PromptTextSizeService service = CreateService(definition);

        service.CurrentTextSize.Should().Be(definition.DefaultValue);
    }

    [Fact]
    public void SetTextSize_WithRegisteredOption_UpdatesValueAndRaisesEvent()
    {
        PromptTextSizeSettingDefinition definition = new();
        PromptTextSizeService service = CreateService(definition);
        double textSize = definition.Options
            .First(option => option.Value > definition.DefaultValue)
            .Value;
        int eventCount = 0;
        service.TextSizeChanged += (_, _) => eventCount++;

        service.SetTextSize(textSize);

        service.CurrentTextSize.Should().Be(textSize);
        eventCount.Should().Be(1);
    }

    [Fact]
    public void SetTextSize_WithUnregisteredValue_ThrowsArgumentOutOfRangeException()
    {
        PromptTextSizeSettingDefinition definition = new();
        PromptTextSizeService service = CreateService(definition);
        double textSize = definition.Options[^1].Value + 1d;

        Action act = () => service.SetTextSize(textSize);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static PromptTextSizeService CreateService(
        PromptTextSizeSettingDefinition definition)
    {
        SettingsDefinitionCatalog catalog = new(
            [definition],
            Array.Empty<IUiScaleOptionDefinition>());

        return new PromptTextSizeService(catalog);
    }
}
