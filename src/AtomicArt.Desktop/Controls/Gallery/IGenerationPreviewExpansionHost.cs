using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AtomicArt.Desktop.Controls.Gallery;

internal interface IGenerationPreviewExpansionHost
{
    bool IsExpansionEnabled { get; }
    Control Viewport { get; }
    KeyModifiers CurrentKeyModifiers { get; }
    Point? PointerPosition { get; }

    event EventHandler? PointerStateChanged;
    event EventHandler? ModifiersChanged;

    void EnableOverflow(Control owner, Visual preview);

    void BeginOverflowCollapse(Control owner);

    void DisableOverflow(Control owner);
}
