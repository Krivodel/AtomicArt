using System.Reflection;

using Avalonia.Controls;
using Avalonia.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class TrayMenuStyleTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void NativeTrayMenuItem_WhenRealContainerIsCreated_UsesHandCursor()
    {
        Dispatch(() =>
        {
            Control trayMenuItem = CreateTrayMenuItemContainer();
            Window window = Show(trayMenuItem);

            try
            {
                trayMenuItem.Should().BeAssignableTo<MenuItem>();
                trayMenuItem.Cursor.Should().NotBeNull();
                trayMenuItem.Cursor!.ToString().Should().Be(StandardCursorType.Hand.ToString());
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Control CreateTrayMenuItemContainer()
    {
        Type presenterType = typeof(NativeMenu).Assembly.GetType(
            "Avalonia.Controls.NativeMenuBarPresenter",
            throwOnError: true)!;
        MethodInfo factory = presenterType.GetMethod(
            "CreateContainerForNativeItem",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Native tray menu item factory was not found.");

        return factory.Invoke(null, [new NativeMenuItem("Show"), 0, null])
                   as Control
               ?? throw new InvalidOperationException("Native tray menu item container was not created.");
    }
}
