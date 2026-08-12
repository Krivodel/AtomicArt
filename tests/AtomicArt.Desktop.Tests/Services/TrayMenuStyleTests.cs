using System.Reflection;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class TrayMenuStyleTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void TrayMenuPresenter_WhenShown_UsesApplicationPalette()
    {
        Dispatch(() =>
        {
            MenuFlyoutPresenter trayMenu = new();
            Window window = Show(trayMenu);

            try
            {
                window.CaptureRenderedFrame();
                Border layoutRoot = trayMenu
                    .GetVisualDescendants()
                    .OfType<Border>()
                    .Single(control => control.Name == "LayoutRoot");
                ISolidColorBrush background = trayMenu.Background
                    .Should()
                    .BeAssignableTo<ISolidColorBrush>()
                    .Subject;
                ISolidColorBrush border = trayMenu.BorderBrush
                    .Should()
                    .BeAssignableTo<ISolidColorBrush>()
                    .Subject;
                Color backgroundColor = GetResource<Color>(
                    trayMenu,
                    "SukiPopupBackground");
                Color borderColor = GetResource<Color>(
                    trayMenu,
                    "SukiMenuBorderBrush");
                Thickness borderThickness = GetResource<Thickness>(
                    trayMenu,
                    "ShellBorderThickness");
                CornerRadius cornerRadius = GetResource<CornerRadius>(
                    trayMenu,
                    "SmallCornerRadius");

                background.Color.Should().Be(backgroundColor);
                border.Color.Should().Be(borderColor);
                layoutRoot.Background.Should().BeSameAs(trayMenu.Background);
                trayMenu.BorderThickness.Should().Be(borderThickness);
                trayMenu.CornerRadius.Should().Be(cornerRadius);
            }
            finally
            {
                window.Close();
            }
        });
    }

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

    [Fact]
    public void NativeTrayMenuItem_WhenSelected_UsesApplicationAccent()
    {
        Dispatch(() =>
        {
            MenuItem trayMenuItem = CreateTrayMenuItemContainer()
                .Should()
                .BeAssignableTo<MenuItem>()
                .Subject;
            trayMenuItem.IsSelected = true;
            Window window = Show(trayMenuItem);

            try
            {
                ISolidColorBrush background = trayMenuItem.Background
                    .Should()
                    .BeAssignableTo<ISolidColorBrush>()
                    .Subject;
                Color accentColor = GetResource<Color>(
                    trayMenuItem,
                    "SukiPrimaryColor5");

                background.Color.Should().Be(accentColor);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Control CreateTrayMenuItemContainer()
    {
        Type presenterType = GetNativeMenuPresenterType();
        MethodInfo factory = presenterType.GetMethod(
            "CreateContainerForNativeItem",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Native tray menu item factory was not found.");

        return factory.Invoke(null, [new NativeMenuItem("Show"), 0, null])
                   as Control
               ?? throw new InvalidOperationException("Native tray menu item container was not created.");
    }

    private static Type GetNativeMenuPresenterType()
    {
        Type? presenterType = typeof(NativeMenu).Assembly.GetType(
            "Avalonia.Controls.NativeMenuBarPresenter",
            throwOnError: true);

        return presenterType
               ?? throw new InvalidOperationException("Native tray menu presenter type was not found.");
    }

    private static T GetResource<T>(Control control, string resourceKey)
        where T : notnull
    {
        bool found = control.TryFindResource(resourceKey, out object? resource);

        found.Should().BeTrue();
        return resource.Should().BeOfType<T>().Subject;
    }
}
