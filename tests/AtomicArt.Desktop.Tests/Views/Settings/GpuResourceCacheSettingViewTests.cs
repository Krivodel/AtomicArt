using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

using FluentAssertions;
using Moq;
using SukiUI.Controls;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Tests.Views.Settings;

public sealed class GpuResourceCacheSettingViewTests : DesktopControlTestBase
{
    [Fact]
    public async Task Layout_BelowSaveFolder_MatchesEditorHeightAndLeftEdge()
    {
        await DispatchAsync(() =>
        {
            GpuResourceCacheOptionViewModel option = new(
                GpuResourceCacheSettingOptions.Options[0], TestLocalizationTextProvider.Default);
            GpuResourceCacheSettingViewModel viewModel = new(
                new GpuResourceCacheSettingDefinition(), [option], option, Mock.Of<ISettingsStateService>(),
                new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default);
            GpuResourceCacheSettingView view = new() { DataContext = viewModel };
            Mock<IAtomicArtDataPathProvider> pathProvider = new();
            pathProvider.SetupGet(provider => provider.RootDirectory).Returns(@"C:\Saves");
            DataRootSettingView folder = new()
            {
                DataContext = new DataRootSettingViewModel(
                    new DataRootSettingDefinition(), Mock.Of<IFolderPickerService>(),
                    Mock.Of<IAtomicArtDataRootMigrationService>(), pathProvider.Object,
                    new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default)
            };
            StackPanel content = new() { Children = { folder, view } };

            Show(content, _ =>
            {
                TextBlock title = view.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Classes.Contains("settings-label"));
                TextBlock notice = view.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Classes.Contains("settings-description"));
                ComboBox dropdown = view.GetVisualDescendants().OfType<ComboBox>().Single();
                GlassCard dropdownFrame = dropdown.GetVisualDescendants().OfType<GlassCard>()
                    .Single(card => card.Name == "border");
                GlassCard folderFrame = folder.GetVisualDescendants().OfType<GlassCard>()
                    .Single(card => card.Name == "PART_GlassBorder");
                TextBlock folderTitle = folder.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Classes.Contains("settings-label"));
                object? captionBrush = view.FindResource("MetadataSecondaryTextBrush");

                title.Foreground.Should().BeSameAs(captionBrush);
                notice.Foreground.Should().BeSameAs(captionBrush);
                (dropdown.TranslatePoint(default, title)?.Y).Should().BeGreaterThan(title.Bounds.Height);
                (dropdownFrame.TranslatePoint(default, content)?.X)
                    .Should().Be(folderFrame.TranslatePoint(default, content)?.X);
                (dropdownFrame.TranslatePoint(default, title)?.Y)
                    .Should().Be(folderFrame.TranslatePoint(default, folderTitle)?.Y);
                dropdownFrame.Bounds.Width.Should().Be(view.Bounds.Width);
                dropdownFrame.Bounds.Height.Should().Be(folderFrame.Bounds.Height);
            });
        });
    }
}
