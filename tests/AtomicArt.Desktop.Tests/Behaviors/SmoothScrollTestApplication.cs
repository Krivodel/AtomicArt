using Avalonia.Themes.Fluent;

namespace AtomicArt.Desktop.Tests.Behaviors;

internal sealed class SmoothScrollTestApplication : Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}
