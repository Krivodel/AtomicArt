using Avalonia.Input;

namespace AtomicArt.Desktop.Controls;

internal static class AlternateActionModifierPolicy
{
    internal static bool IsActive(KeyModifiers modifiers)
    {
        KeyModifiers alternateActionModifiers = KeyModifiers.Shift
            | KeyModifiers.Control
            | KeyModifiers.Alt;

        return (modifiers & alternateActionModifiers) != KeyModifiers.None;
    }
}
