using Avalonia.Controls;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Views.Gallery;

internal sealed class GenerationCardControlFactory : IGalleryCardControlFactory
{
    private const int MaximumPooledControlCount = 64;

    private readonly IGalleryPreviewBitmapProvider _previewBitmapProvider;
    private readonly GalleryPreviewSourceScheduler _previewSourceScheduler;
    private readonly UiAnimationScheduler _animationScheduler;
    private readonly Stack<GenerationCardControl> _pooledControls = [];

    public GenerationCardControlFactory(
        IGalleryPreviewBitmapProvider previewBitmapProvider,
        GalleryPreviewSourceScheduler previewSourceScheduler,
        UiAnimationScheduler animationScheduler)
    {
        _previewBitmapProvider = previewBitmapProvider
            ?? throw new ArgumentNullException(nameof(previewBitmapProvider));
        _previewSourceScheduler = previewSourceScheduler
            ?? throw new ArgumentNullException(nameof(previewSourceScheduler));
        _animationScheduler = animationScheduler
            ?? throw new ArgumentNullException(nameof(animationScheduler));
    }

    public Control Create(
        object item,
        GalleryCardCommands commands,
        IGenerationPreviewExpansionHost previewExpansionHost)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(previewExpansionHost);

        GenerationCardControl control = _pooledControls.Count > 0
            ? _pooledControls.Pop()
            : new GenerationCardControl();
        PrepareControl(control, commands, previewExpansionHost);

        return control;
    }

    public Control CreateTransient(
        object item,
        GalleryCardCommands commands,
        IGenerationPreviewExpansionHost previewExpansionHost)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(previewExpansionHost);

        GenerationCardControl control = new();
        PrepareControl(control, commands, previewExpansionHost);

        return control;
    }

    public void ApplyCommands(Control control, GalleryCardCommands commands)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(commands);

        GenerationCardControl generationCard = RequireGenerationCard(control);

        generationCard.OpenViewerCommand = commands.OpenViewer;
        generationCard.RevealInFolderCommand = commands.RevealInFolder;
        generationCard.RevealInNewFolderWindowCommand =
            commands.RevealInNewFolderWindow;
        generationCard.OpenMetadataCommand = commands.OpenMetadata;
        generationCard.DeleteOrCancelCommand = commands.DeleteOrCancel;
    }

    public bool CanRetainRecycledControl(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        RequireGenerationCard(control);

        return _pooledControls.Count < MaximumPooledControlCount;
    }

    public void Recycle(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        GenerationCardControl generationCard = RequireGenerationCard(control);

        _animationScheduler.Cancel(generationCard);

        if (generationCard.RenderTransform is not null)
        {
            MotionFrameApplier.Apply(generationCard, MotionFrame.Identity);
        }
        else
        {
            generationCard.Opacity = 1d;
        }

        generationCard.ZIndex = 0;
        generationCard.DataContext = null;
        generationCard.IsVisible = false;

        if (_pooledControls.Count < MaximumPooledControlCount)
        {
            _pooledControls.Push(generationCard);
        }
    }

    private static GenerationCardControl RequireGenerationCard(Control control)
    {
        if (control is GenerationCardControl generationCard)
        {
            return generationCard;
        }

        throw new ArgumentException(
            $"Expected {nameof(GenerationCardControl)} control.",
            nameof(control));
    }

    private void PrepareControl(
        GenerationCardControl control,
        GalleryCardCommands commands,
        IGenerationPreviewExpansionHost previewExpansionHost)
    {
        control.IsVisible = true;
        control.PreviewExpansionHost = previewExpansionHost;
        control.SetPreviewBitmapServices(
            _previewBitmapProvider,
            _previewSourceScheduler);
        ApplyCommands(control, commands);
    }
}
