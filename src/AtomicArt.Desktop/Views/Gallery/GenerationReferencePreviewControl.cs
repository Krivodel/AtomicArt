using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Views.Gallery;

public sealed class GenerationReferencePreviewControl : Image
{
    public GenerationReference? Reference
    {
        get => GetValue(ReferenceProperty);
        set => SetValue(ReferenceProperty, value);
    }
    public GenerationReferencePreviewLoader? Loader
    {
        get => GetValue(LoaderProperty);
        set => SetValue(LoaderProperty, value);
    }
    public int? MaximumPreviewPixels
    {
        get => GetValue(MaximumPreviewPixelsProperty);
        set => SetValue(MaximumPreviewPixelsProperty, value);
    }
    public bool LoadInViewport
    {
        get => GetValue(LoadInViewportProperty);
        set => SetValue(LoadInViewportProperty, value);
    }
    public bool IsNearViewport => _isNearViewport;

    public static readonly StyledProperty<GenerationReference?> ReferenceProperty =
        AvaloniaProperty.Register<GenerationReferencePreviewControl, GenerationReference?>(nameof(Reference));
    public static readonly StyledProperty<GenerationReferencePreviewLoader?> LoaderProperty =
        AvaloniaProperty.Register<GenerationReferencePreviewControl, GenerationReferencePreviewLoader?>(nameof(Loader));
    public static readonly StyledProperty<int?> MaximumPreviewPixelsProperty =
        AvaloniaProperty.Register<GenerationReferencePreviewControl, int?>(nameof(MaximumPreviewPixels), validate: value => value is null or > 0);
    public static readonly StyledProperty<bool> LoadInViewportProperty =
        AvaloniaProperty.Register<GenerationReferencePreviewControl, bool>(nameof(LoadInViewport));
    public static readonly DirectProperty<GenerationReferencePreviewControl, bool> IsNearViewportProperty =
        AvaloniaProperty.RegisterDirect<GenerationReferencePreviewControl, bool>(
            nameof(IsNearViewport), control => control.IsNearViewport);

    internal Task PreviewLoad { get; private set; } = Task.CompletedTask;

    private const double ViewportBuffer = 128d;

    private CancellationTokenSource? _loading;
    private GalleryPreviewBitmapLease? _preview;
    private bool _isAttached;
    private bool _isNearViewport;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        SetAndRaise(IsNearViewportProperty, ref _isNearViewport, false);
        ClearPreview();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            PseudoClasses.Set(":decoded", Source is not null);
        }
        else if (change.Property == LoaderProperty || change.Property == ReferenceProperty
            || change.Property == MaximumPreviewPixelsProperty || change.Property == LoadInViewportProperty)
        {
            if (change.Property == LoadInViewportProperty)
            {
                EffectiveViewportChanged -= OnEffectiveViewportChanged;

                if (LoadInViewport)
                {
                    EffectiveViewportChanged += OnEffectiveViewportChanged;
                }
            }

            Refresh();
        }
    }

    private void Refresh()
    {
        ClearPreview();

        if (_isAttached && (!LoadInViewport || IsNearViewport) && Loader is not null && Reference is not null)
        {
            CancellationTokenSource loading = new();
            _loading = loading;
            PreviewLoad = LoadAsync(Loader, Reference, loading);
        }
    }

    private void ClearPreview()
    {
        _loading?.Cancel();
        _loading = null;
        PreviewLoad = Task.CompletedTask;
        Source = null;
        _preview?.Dispose();
        _preview = null;
    }

    private async Task LoadAsync(
        GenerationReferencePreviewLoader loader,
        GenerationReference reference,
        CancellationTokenSource loading)
    {
        try
        {
            GalleryPreviewBitmapLease? preview = await loader.AcquireAsync(reference, loading.Token, MaximumPreviewPixels);

            if (ReferenceEquals(_loading, loading))
            {
                _preview = preview;
                Source = preview?.Bitmap;
            }
            else
            {
                preview?.Dispose();
            }
        }
        catch (OperationCanceledException) when (loading.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_loading, loading))
            {
                _loading = null;
            }

            loading.Dispose();
        }
    }

    private void OnEffectiveViewportChanged(object? sender, Avalonia.Layout.EffectiveViewportChangedEventArgs e)
    {
        Rect bufferedBounds = new Rect(Bounds.Size).Inflate(ViewportBuffer);

        if (SetAndRaise(IsNearViewportProperty, ref _isNearViewport, bufferedBounds.Intersects(e.EffectiveViewport)))
        {
            Refresh();
        }
    }
}
