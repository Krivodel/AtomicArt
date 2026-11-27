using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Notifications;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.ViewModels.Settings;

public sealed class GenerationNotificationsSettingViewModelTests
{
    [Fact]
    public async Task IsChecked_WhenDisabled_SavesAndRestoresTheSetting()
    {
        GenerationNotificationsSettingDefinition definition = new();
        GenerationNotificationSettings options = new();
        BooleanSettingValueConverter converter = new();
        GenerationNotificationsSettingsStateApplicator applicator = new(options, converter);
        Mock<ISettingsStateService> state = new();
        state.Setup(service => service.ApplyValue(definition, It.IsAny<string>()))
            .Callback<ISettingsDefinition, string>((_, value) => applicator.Apply(value));
        state.Setup(service => service.SaveValueAsync(definition, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        using GenerationNotificationsSettingViewModel editor = new(definition, options, state.Object, converter,
            Mock.Of<INotificationSoundStore>(), Mock.Of<ISoundFilePickerService>(),
            new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default);

        editor.IsChecked = false;
        await (editor.ApplyCommand.ExecutionTask ?? Task.CompletedTask);

        options.Enabled.Should().BeFalse();
        state.Verify(service => service.SaveValueAsync(definition, "False", It.IsAny<CancellationToken>()), Times.Once);
        GenerationNotificationSettings restored = new();
        new GenerationNotificationsSettingsStateApplicator(restored, converter).Apply("False");
        restored.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UploadSoundCommand_WhenPickerIsCanceled_KeepsTheExistingSound()
    {
        Mock<INotificationSoundStore> sounds = new();
        sounds.SetupGet(store => store.HasCustomSound).Returns(true);
        Mock<ISoundFilePickerService> picker = new();
        picker.Setup(service => service.PickSoundAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        using GenerationNotificationsSettingViewModel editor = new(new GenerationNotificationsSettingDefinition(),
            new GenerationNotificationSettings(), Mock.Of<ISettingsStateService>(), new BooleanSettingValueConverter(),
            sounds.Object, picker.Object, new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default);

        await editor.UploadSoundCommand.ExecuteAsync(null);

        editor.HasCustomSound.Should().BeTrue();
        sounds.Verify(store => store.ImportAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sounds.Verify(store => store.ResetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
