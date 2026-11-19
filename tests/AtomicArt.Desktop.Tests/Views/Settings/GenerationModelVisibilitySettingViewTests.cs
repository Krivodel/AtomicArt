using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
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
    [InlineData("openrouter-nano-banana-2", null)]
    [InlineData("nano-banana-2", null)]
    [InlineData("openrouter-nano-banana-2", "openrouter-gpt-image-2")]
    public async Task Click_OnModelProvider_ChangesOnlySelectedModelAndSavesOnce(string modelId, string? omittedModelId)
    {
        await DispatchAsync(async () =>
        {
            GenerationModelVisibilitySettingDefinition definition = new();
            GenerationModelVisibilityService visibility = new();
            IReadOnlyList<string> initiallyVisible = visibility.GetVisibleModelIds();
            Mock<ISettingsStateService> settings = new();
            settings.Setup(service => service.SaveValueAsync(
                definition, It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            using GenerationModelVisibilitySettingViewModel editor = CreateEditor(
                definition, visibility, settings.Object, omittedModelId);
            await editor.LoadCommand.ExecuteAsync(null);
            GenerationModelVisibilitySettingView view = new() { DataContext = editor };
            Window window = Show(view);

            try
            {
                GenerationModelVisibilityRowViewModel row = editor.Rows.Single(item => item.DisplayName == "Nano Banana 2");
                GenerationModelVisibilityOptionViewModel model = row.Options.Single(option => option.ModelId == modelId);
                CheckBox checkBox = view.GetVisualDescendants().OfType<CheckBox>()
                    .Single(box => ReferenceEquals(box.DataContext, model));

                CheckBoxTestInput.Click(window, checkBox, "indicator");
                await (editor.SaveCommand.ExecutionTask ?? Task.CompletedTask);

                model.IsVisible.Should().BeFalse();
                visibility.IsVisible(model.ModelId).Should().BeFalse();
                visibility.GetVisibleModelIds().Should().Equal(initiallyVisible.Where(id => id != modelId));
                editor.Rows.SelectMany(item => item.Options)
                    .Where(option => option.ModelId != modelId)
                    .Should().OnlyContain(option => option.IsVisible);
                settings.Verify(service => service.SaveValueAsync(
                    definition, visibility.GetSerializedValue(), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ModelName_WhenProviderCheckboxIsHovered_HighlightsOnlyMatchingRow()
    {
        await DispatchAsync(async () =>
        {
            using GenerationModelVisibilitySettingViewModel editor = CreateEditor(
                new GenerationModelVisibilitySettingDefinition(), new GenerationModelVisibilityService(), Mock.Of<ISettingsStateService>());
            await editor.LoadCommand.ExecuteAsync(null);
            GenerationModelVisibilitySettingView view = new() { DataContext = editor };

            Show(view, window =>
            {
                CheckBox checkbox = view.GetVisualDescendants().OfType<CheckBox>().First();
                Border row = checkbox.GetVisualAncestors().OfType<Border>()
                    .Single(border => border.Classes.Contains("settings-model-row"));
                TextBlock name = row.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Classes.Contains("settings-model-name"));
                Point center = checkbox.TranslatePoint(new Point(checkbox.Bounds.Width / 2d, checkbox.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The model checkbox is not attached.");
                object highlightBrush = view.FindResource("MetadataRepeatBrush")
                    ?? throw new InvalidOperationException("The model highlight brush was not found.");

                window.MouseMove(center, RawInputModifiers.None);
                window.CaptureRenderedFrame();

                name.Foreground.Should().BeSameAs(highlightBrush);
                view.GetVisualDescendants().OfType<TextBlock>()
                    .Where(text => text.Classes.Contains("settings-model-name") && !ReferenceEquals(text, name))
                    .Should().OnlyContain(text => !ReferenceEquals(text.Foreground, highlightBrush));
            });
        });
    }

    [Fact]
    public async Task Rows_WhenShown_SeparateOnlyModelFamilies()
    {
        await DispatchAsync(async () =>
        {
            using GenerationModelVisibilitySettingViewModel editor = CreateEditor(
                new GenerationModelVisibilitySettingDefinition(), new GenerationModelVisibilityService(), Mock.Of<ISettingsStateService>());
            await editor.LoadCommand.ExecuteAsync(null);
            GenerationModelVisibilitySettingView view = new() { DataContext = editor };

            Show(view, _ =>
            {
                Border divider = view.GetVisualDescendants().OfType<Border>()
                    .Where(border => border.Classes.Contains("settings-model-row")
                        && border.BorderThickness != default)
                    .Should().ContainSingle().Subject;

                divider.DataContext.Should().BeOfType<GenerationModelVisibilityRowViewModel>()
                    .Which.DisplayName.Should().Be("Nano Banana 2");
                divider.BorderThickness.Should().Be(new Thickness(0d, 1d, 0d, 0d));
            });
        });
    }

    private static GenerationModelVisibilitySettingViewModel CreateEditor(
        GenerationModelVisibilitySettingDefinition definition,
        GenerationModelVisibilityService visibility,
        ISettingsStateService settings,
        string? omittedModelId = null)
    {
        ImageModelOptionCatalog catalog = new();
        GenerationModelCatalogDto metadata = ApiModelMetadataTestCatalog.LoadCatalog();
        catalog.Initialize(metadata with { Models = metadata.Models.Where(model => model.Id != omittedModelId).ToArray() });
        return new GenerationModelVisibilitySettingViewModel(
            definition, Mock.Of<IGenerationModelCatalogApiClient>(), catalog, visibility,
            settings, new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default);
    }
}
