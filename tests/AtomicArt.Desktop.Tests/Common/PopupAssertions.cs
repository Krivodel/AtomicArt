using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

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

        PopupRoot popupRoot = TopLevel.GetTopLevel(content).Should()
            .BeOfType<PopupRoot>()
            .Subject;
        ScaleTransform inheritedScale = popupRoot.Transform.Should()
            .BeOfType<ScaleTransform>()
            .Subject;

        popup.InheritsTransform.Should().BeTrue();
        inheritedScale.ScaleX.Should().BeApproximately(expectedScale, 0.001d);
        inheritedScale.ScaleY.Should().BeApproximately(expectedScale, 0.001d);
    }
}
