using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

using FluentAssertions;

namespace AtomicArt.Desktop.Tests.Common;

internal static class PopupAssertions
{
    public static void AssertInheritsScale(
        Popup popup,
        Control content,
        double expectedScale)
    {
        ArgumentNullException.ThrowIfNull(popup);
        ArgumentNullException.ThrowIfNull(content);

        Transform? popupHostTransform = GetPopupHostTransform(content);
        ScaleTransform inheritedScale = popupHostTransform.Should()
            .BeOfType<ScaleTransform>()
            .Subject;

        popup.InheritsTransform.Should().BeTrue();
        inheritedScale.ScaleX.Should().BeApproximately(expectedScale, 0.001d);
        inheritedScale.ScaleY.Should().BeApproximately(expectedScale, 0.001d);
    }

    private static Transform? GetPopupHostTransform(Control content)
    {
        if (TopLevel.GetTopLevel(content) is PopupRoot popupRoot)
        {
            return popupRoot.Transform;
        }

        OverlayPopupHost overlayPopupHost = content
            .GetVisualAncestors()
            .OfType<OverlayPopupHost>()
            .Single();

        return overlayPopupHost.Transform;
    }
}
