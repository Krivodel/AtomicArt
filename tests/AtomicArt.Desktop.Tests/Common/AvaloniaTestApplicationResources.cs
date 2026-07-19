using Avalonia;
using Avalonia.Markup.Xaml.Styling;

namespace AtomicArt.Desktop.Tests.Common;

internal static class AvaloniaTestApplicationResources
{
    public static void AddResourceIncludes(
        Avalonia.Application application,
        Uri baseUri,
        IReadOnlyList<string> resourcePaths)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(resourcePaths);

        foreach (string resourcePath in resourcePaths)
        {
            application.Resources.MergedDictionaries.Add(new ResourceInclude(baseUri)
            {
                Source = new Uri(resourcePath)
            });
        }
    }

    public static void AddStyleIncludes(
        Avalonia.Application application,
        Uri baseUri,
        IReadOnlyList<string> stylePaths)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(stylePaths);

        foreach (string stylePath in stylePaths)
        {
            application.Styles.Add(new StyleInclude(baseUri)
            {
                Source = new Uri(stylePath)
            });
        }
    }
}
