using System.Collections;

using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryAnimationTracker : IEnumerable<Control>
{
    private readonly List<Control> _controls = [];

    public IEnumerator<Control> GetEnumerator()
    {
        return _controls.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal void Add(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        _controls.Add(control);
    }

    internal void Remove(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        _controls.Remove(control);
    }

    internal void Clear()
    {
        _controls.Clear();
    }
}
