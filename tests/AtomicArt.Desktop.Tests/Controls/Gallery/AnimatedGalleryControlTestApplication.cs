using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

using SukiUI;


namespace AtomicArt.Desktop.Tests.Controls.Gallery;

internal sealed class AnimatedGalleryControlTestApplication : Avalonia.Application
{
    public override void Initialize()
    {
        Uri baseUri = new("avares://AtomicArt/");
        RequestedThemeVariant = ThemeVariant.Dark;

        Resources.MergedDictionaries.Add(new ResourceInclude(baseUri)
        {
            Source = new Uri("avares://AtomicArt/Resources/Colors.axaml")
        });
        Resources.MergedDictionaries.Add(new ResourceInclude(baseUri)
        {
            Source = new Uri("avares://AtomicArt/Resources/Dimensions.axaml")
        });
        Resources.MergedDictionaries.Add(new ResourceInclude(baseUri)
        {
            Source = new Uri("avares://AtomicArt/Resources/Icons.axaml")
        });
        Resources.MergedDictionaries.Add(new ResourceInclude(baseUri)
        {
            Source = new Uri("avares://AtomicArt/Resources/Converters.axaml")
        });
        Styles.Add(new FluentTheme());
        Styles.Add(new SukiTheme());
        Styles.Add(new StyleInclude(baseUri)
        {
            Source = new Uri("avares://AtomicArt/Resources/ComboBoxStyles.axaml")
        });
        Styles.Add(new StyleInclude(baseUri)
        {
            Source = new Uri("avares://AtomicArt/Resources/TextStyles.axaml")
        });
        Styles.Add(new StyleInclude(baseUri)
        {
            Source = new Uri("avares://AtomicArt/Resources/ButtonStyles.axaml")
        });
        Styles.Add(new StyleInclude(baseUri)
        {
            Source = new Uri("avares://AtomicArt/Resources/ScrollViewerStyles.axaml")
        });
        Styles.Add(new StyleInclude(baseUri)
        {
            Source = new Uri("avares://AtomicArt/Resources/Templates.axaml")
        });
    }
}
