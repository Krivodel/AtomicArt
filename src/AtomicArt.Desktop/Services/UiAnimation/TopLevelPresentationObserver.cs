using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed class TopLevelPresentationObserver : IDisposable
{
    public bool IsAttached => _topLevel is not null;
    public bool IsPresented { get; private set; }

    private readonly Action<bool> _presentationChanged;
    private TopLevel? _topLevel;

    public TopLevelPresentationObserver(Action<bool> presentationChanged)
    {
        _presentationChanged = presentationChanged
            ?? throw new ArgumentNullException(nameof(presentationChanged));
    }

    public void Attach(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);

        TopLevel? topLevel = TopLevel.GetTopLevel(visual);

        if (ReferenceEquals(_topLevel, topLevel))
        {
            IsPresented = WindowPresentationState.IsPresented(topLevel);
            return;
        }

        Detach();
        _topLevel = topLevel;

        if (_topLevel is not null)
        {
            _topLevel.PropertyChanged += OnTopLevelPropertyChanged;
        }

        IsPresented = WindowPresentationState.IsPresented(_topLevel);
    }

    public void Detach()
    {
        if (_topLevel is not null)
        {
            _topLevel.PropertyChanged -= OnTopLevelPropertyChanged;
            _topLevel = null;
        }

        IsPresented = false;
    }

    public void Dispose()
    {
        Detach();
    }

    private void UpdatePresentation()
    {
        bool isPresented = WindowPresentationState.IsPresented(_topLevel);

        if (isPresented == IsPresented)
        {
            return;
        }

        IsPresented = isPresented;
        _presentationChanged(isPresented);
    }

    private void OnTopLevelPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        _ = sender;

        if (e.Property == Visual.IsVisibleProperty
            || e.Property == Window.WindowStateProperty)
        {
            UpdatePresentation();
        }
    }
}
