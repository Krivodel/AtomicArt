using Avalonia.Controls;
using Avalonia.VisualTree;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Tests.Views.Settings;

public sealed class GenerationModelVisibilitySettingViewTests : DesktopControlTestBase
{
    [Theory]
    [InlineData("label")]
    [InlineData("indicator")]
    [InlineData("space")]
    public async Task Click_OnAnyPartOfModelRow_ChangesOnlySelectedModelAndSavesOnce(string target)
    {
        await DispatchAsync(async () =>
        {
            ImageModelOptionCatalog catalog = new();
            catalog.Initialize(ApiModelMetadataTestCatalog.LoadCatalog());
            GenerationModelVisibilitySettingDefinition definition = new();
            GenerationModelVisibilityService visibility = new();
            Mock<ISettingsStateService> settings = new();
            settings.Setup(service => service.SaveValueAsync(
                definition, It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            using GenerationModelVisibilitySettingViewModel editor = new(
                definition, new Mock<IGenerationModelCatalogApiClient>().Object, catalog, visibility,
                settings.Object, new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default);
            await editor.LoadCommand.ExecuteAsync(null);
            GenerationModelVisibilitySettingView view = new() { DataContext = editor };
            Window window = Show(view);

            try
            {
                CheckBox checkBox = view.GetVisualDescendants().OfType<CheckBox>().First();
                GenerationModelVisibilityOptionViewModel model = editor.Models.First();
                checkBox.Bounds.Width.Should().Be(view.Bounds.Width);

                CheckBoxTestInput.Click(window, checkBox, target);
                await (editor.SaveCommand.ExecutionTask ?? Task.CompletedTask);

                model.IsVisible.Should().BeFalse();
                visibility.IsVisible(model.ModelId).Should().BeFalse();
                editor.Models.Skip(1).Should().OnlyContain(item => item.IsVisible);
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
