using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GenerationPreviewOverflowController
{
    private readonly Control _viewport;
    private readonly Dictionary<Control, (int ZIndex, IReadOnlyList<Visual> Path)> _owners = [];
    private readonly Dictionary<Visual, (int References, bool ClipToBounds, Geometry? Clip)> _clips = [];

    public GenerationPreviewOverflowController(Control viewport)
    {
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
    }

    public void Enable(Control owner, Visual preview)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(preview);

        if (!_owners.ContainsKey(owner))
        {
            IReadOnlyList<Visual> path = GetOverflowPath(preview);
            _owners.Add(owner, (owner.ZIndex, path));

            foreach (Visual visual in path)
            {
                if (_clips.TryGetValue(visual, out (int References, bool ClipToBounds, Geometry? Clip) state))
                {
                    _clips[visual] = (state.References + 1, state.ClipToBounds, state.Clip);
                }
                else
                {
                    _clips.Add(visual, (1, visual.ClipToBounds, visual.Clip));
                    visual.ClipToBounds = false;
                    visual.Clip = null;
                }
            }
        }

        owner.ZIndex = GenerationPreviewExpansionVisualMetrics.ActiveZIndex;
    }

    public void BeginCollapse(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (_owners.ContainsKey(owner))
        {
            owner.ZIndex = GenerationPreviewExpansionVisualMetrics.CollapsingZIndex;
        }
    }

    public void Disable(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (!_owners.Remove(owner, out (int ZIndex, IReadOnlyList<Visual> Path) saved))
        {
            return;
        }

        owner.ZIndex = saved.ZIndex;

        foreach (Visual visual in saved.Path)
        {
            (int references, bool clipToBounds, Geometry? clip) = _clips[visual];

            if (references > 1)
            {
                _clips[visual] = (references - 1, clipToBounds, clip);
            }
            else
            {
                visual.ClipToBounds = clipToBounds;
                visual.Clip = clip;
                _clips.Remove(visual);
            }
        }
    }

    public void Reset()
    {
        foreach (KeyValuePair<Control, (int ZIndex, IReadOnlyList<Visual> Path)> owner in _owners)
        {
            owner.Key.ZIndex = owner.Value.ZIndex;
            owner.Key.Classes.Remove(GenerationPreviewExpansionVisualMetrics.ExpandedClass);
        }

        foreach (KeyValuePair<Visual, (int References, bool ClipToBounds, Geometry? Clip)> visual in _clips)
        {
            visual.Key.ClipToBounds = visual.Value.ClipToBounds;
            visual.Key.Clip = visual.Value.Clip;
        }

        _owners.Clear();
        _clips.Clear();
    }

    private IReadOnlyList<Visual> GetOverflowPath(Visual preview)
    {
        List<Visual> path = [];
        Visual? current = preview;

        while (current is not null && !object.ReferenceEquals(current, _viewport))
        {
            path.Add(current);
            current = current.GetVisualParent();
        }

        if (current is null)
        {
            throw new InvalidOperationException("The generation preview is outside its expansion viewport.");
        }

        return path;
    }
}
