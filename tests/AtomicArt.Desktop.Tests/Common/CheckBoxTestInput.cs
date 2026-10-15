using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

using SukiUI.Controls;

namespace AtomicArt.Desktop.Tests.Common;

internal static class CheckBoxTestInput
{
    public static void Click(Window window, CheckBox checkBox, string target)
    {
        Point point = GetPoint(window, checkBox, target);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static Point GetPoint(Window window, CheckBox checkBox, string target)
    {
        Control element = target switch
        {
            "label" => checkBox.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(presenter => presenter.Name == "PART_ContentPresenter"),
            "indicator" => checkBox.GetVisualDescendants().OfType<GlassCard>()
                .Single(card => card.Name == "border"),
            "space" => checkBox,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown checkbox click target.")
        };
        double fraction = target == "space" ? 0.75d : 0.5d;

        return element.TranslatePoint(new Point(element.Bounds.Width * fraction, element.Bounds.Height / 2d), window)
            ?? throw new InvalidOperationException("The checkbox click target is not attached to the test window.");
    }
}
