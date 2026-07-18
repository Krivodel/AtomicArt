using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

using SukiUI;

namespace AtomicArt.Desktop.Tests.Behaviors;

internal sealed class ComboBoxAnimationTestApplication : Avalonia.Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Uri baseUri = new("avares://AtomicArt/");

        Resources.MergedDictionaries.Add(new ResourceInclude(baseUri)
        {
            Source = new Uri("avares://AtomicArt/Resources/Colors.axaml")
        });
        Styles.Add(new FluentTheme());
        Styles.Add(new SukiTheme());
        Styles.Add(new StyleInclude(baseUri)
        {
            Source = new Uri("avares://AtomicArt/Resources/ComboBoxStyles.axaml")
        });
    }
}
