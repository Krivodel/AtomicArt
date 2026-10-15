using Avalonia.Controls;
using Avalonia.VisualTree;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Tests.Views.Settings;

public sealed class BooleanSettingViewTests : DesktopControlTestBase
{
    [Theory]
    [InlineData("label")]
    [InlineData("indicator")]
    [InlineData("space")]
    public async Task Click_OnAnyPartOfSetting_AppliesAndSavesOnce(string target)
    {
        await DispatchAsync(async () =>
        {
            ConfirmDeletionSettingDefinition definition = new();
            Mock<IBooleanSettingValueSource> source = new();
            source.SetupGet(value => value.CurrentValue).Returns(true);
            Mock<ISettingsStateService> settings = new();
            settings.Setup(service => service.SaveValueAsync(definition, "False", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            using BooleanSettingViewModel editor = new(
                definition, source.Object, settings.Object, new BooleanSettingValueConverter(),
                new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default);
            BooleanSettingView view = new() { DataContext = editor };
            Window window = Show(view);

            try
            {
                CheckBox checkBox = view.GetVisualDescendants().OfType<CheckBox>().Single();

                CheckBoxTestInput.Click(window, checkBox, target);
                await (editor.ApplyCommand.ExecutionTask ?? Task.CompletedTask);

                checkBox.Content.Should().Be(editor.DisplayName);
                editor.IsChecked.Should().BeFalse();
                settings.Verify(service => service.SaveValueAsync(
                    definition, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
