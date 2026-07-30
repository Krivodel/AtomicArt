using Avalonia.Styling;
using Avalonia.Themes.Fluent;

using SukiUI;

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
        "avares://AtomicArt/Resources/ComboBoxStyles.axaml",
        "avares://AtomicArt/Resources/TextStyles.axaml",
        "avares://AtomicArt/Resources/ButtonStyles.axaml",
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
        Styles.Add(new SukiTheme());
        AvaloniaTestApplicationResources.AddStyleIncludes(
            this,
            baseUri,
            StylePaths);
    }
}
