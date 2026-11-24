using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using SukiUI;
using SukiUI.Enums;

namespace AtomicArt.Desktop.Tests.Common;

internal sealed class DesktopControlTestApplication : Avalonia.Application
{
    private static readonly string[] ResourcePaths =
        [
            "avares://AtomicArt/Resources/SharedResources.axaml",
            "avares://AtomicArt/Resources/Converters.axaml"
        ];
    private static readonly string[] StylePaths =
        [
            "avares://AtomicArt/Resources/SharedControlStyles.axaml",
            "avares://AtomicArt/Resources/Dlss5Styles.axaml",
            "avares://AtomicArt/Resources/GenerationChatStyles.axaml",
            "avares://AtomicArt/Controls/Themes/Generic.axaml",
            "avares://AtomicArt/Resources/ScrollViewerStyles.axaml",
            "avares://AtomicArt/Resources/TextBoxStyles.axaml",
            "avares://AtomicArt/Resources/Templates.axaml"
        ];

    public override void Initialize()
    {
        Uri baseUri = new("avares://AtomicArt/");
        RequestedThemeVariant = ThemeVariant.Dark;

        AvaloniaTestApplicationResources.AddResourceIncludes(
            this,
            baseUri,
            ResourcePaths);
        Styles.Add(new FluentTheme());
        Styles.Add(new SukiTheme { ThemeColor = SukiColor.Blue });
        AvaloniaTestApplicationResources.AddStyleIncludes(
            this,
            baseUri,
            StylePaths);
    }
}
