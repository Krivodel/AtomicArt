using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Views.Gallery;

public sealed class GenerationReferencePreviewControl : Image
{
    public static readonly StyledProperty<GenerationReference?> ReferenceProperty =
        AvaloniaProperty.Register<GenerationReferencePreviewControl, GenerationReference?>(nameof(Reference));
    public static readonly StyledProperty<GenerationReferencePreviewLoader?> LoaderProperty =
        AvaloniaProperty.Register<GenerationReferencePreviewControl, GenerationReferencePreviewLoader?>(nameof(Loader));

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

    private CancellationTokenSource? _loading;
    private bool _isAttached;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
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
        else if (change.Property == LoaderProperty || change.Property == ReferenceProperty)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        ClearPreview();
        if (_isAttached && Loader is not null && Reference is not null)
        {
            CancellationTokenSource loading = new();
            _loading = loading;
            _ = LoadAsync(Loader, Reference, loading);
        }
    }

    private void ClearPreview()
    {
        _loading?.Cancel();
        _loading = null;
        Bitmap? bitmap = Source as Bitmap;
        Source = null;
        bitmap?.Dispose();
    }

    private async Task LoadAsync(
        GenerationReferencePreviewLoader loader,
        GenerationReference reference,
        CancellationTokenSource loading)
    {
        try
        {
            Bitmap? bitmap = await loader.LoadAsync(reference, loading.Token);
            if (ReferenceEquals(_loading, loading))
            {
                Source = bitmap;
            }
            else
            {
                bitmap?.Dispose();
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
}
