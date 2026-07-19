using Avalonia.Styling;
using Avalonia.Themes.Fluent;

using SukiUI;

using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Behaviors;

internal sealed class ComboBoxAnimationTestApplication : Avalonia.Application
{
    private const string ColorsResourcePath = "avares://AtomicArt/Resources/Colors.axaml";
    private const string ComboBoxStylesPath = "avares://AtomicArt/Resources/ComboBoxStyles.axaml";

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Uri baseUri = new("avares://AtomicArt/");

        AvaloniaTestApplicationResources.AddResourceIncludes(
            this,
            baseUri,
            new string[] { ColorsResourcePath });
        Styles.Add(new FluentTheme());
        Styles.Add(new SukiTheme());
        AvaloniaTestApplicationResources.AddStyleIncludes(
            this,
            baseUri,
            new string[] { ComboBoxStylesPath });
    }
}
