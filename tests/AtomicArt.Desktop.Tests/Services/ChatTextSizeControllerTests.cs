using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ChatTextSizeControllerTests
{
    [Fact]
    public void Apply_WithStoredSize_PreservesUserChoiceAndDefaultsNewChatToSixteen()
    {
        SettingsDefinitionCatalog catalog = new(
            new ISettingsDefinition[] { new ChatTextSizeSettingDefinition() }, Array.Empty<IUiScaleOptionDefinition>());
        ChatTextSizeService chat = new(catalog);
        chat.CurrentTextSize.Should().Be(16d);

        new ChatTextSizeSettingsStateApplicator(catalog, chat, new DoubleSettingValueConverter()).Apply("14");

        chat.CurrentTextSize.Should().Be(14d);
    }

    [Theory]
    [InlineData(TextSizeAdjustment.Increase, 17d)]
    [InlineData(TextSizeAdjustment.Decrease, 15d)]
    public async Task AdjustAsync_WithOpenSettings_SavesChatValueAndKeepsPromptValue(
        TextSizeAdjustment adjustment, double expected)
    {
        ChatTextSizeSettingDefinition definition = new();
        SettingsDefinitionCatalog catalog = new(
            new ISettingsDefinition[] { definition, new PromptTextSizeSettingDefinition() },
            Array.Empty<IUiScaleOptionDefinition>());
        ChatTextSizeService chat = new(catalog);
        PromptTextSizeService prompt = new(catalog);
        DoubleSettingValueConverter converter = new();
        ChatTextSizeSettingsStateApplicator applicator = new(catalog, chat, converter);
        Mock<ISettingsStateService> settings = new();
        settings.Setup(service => service.ApplyValue(definition, It.IsAny<string>()))
            .Callback<ISettingsDefinition, string>((_, value) => applicator.Apply(value));
        ChatTextSizeController controller = new(catalog, chat, settings.Object, converter);
        ChatTextSizeSettingViewModelFactory factory = new(chat, settings.Object, converter,
            new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default);
        using NumericSettingViewModel editor = Assert.IsType<NumericSettingViewModel>(factory.Create(definition));

        await controller.AdjustAsync(adjustment, CancellationToken.None);

        chat.CurrentTextSize.Should().Be(expected);
        editor.SelectedOption?.Value.Should().Be(expected);
        prompt.CurrentTextSize.Should().Be(14d);
        settings.Verify(service => service.SaveValueAsync(definition, converter.Format(expected),
            It.IsAny<CancellationToken>()), Times.Once);
        ChatTextSizeService restored = new(catalog);
        new ChatTextSizeSettingsStateApplicator(catalog, restored, converter).Apply(converter.Format(expected));
        restored.CurrentTextSize.Should().Be(expected);
    }
}
