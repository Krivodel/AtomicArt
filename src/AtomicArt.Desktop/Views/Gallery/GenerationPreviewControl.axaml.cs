using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Views.Gallery;

public partial class GenerationPreviewControl : UserControl
{
    public static readonly StyledProperty<IRelayCommand?> OpenViewerCommandProperty =
        AvaloniaProperty.Register<GenerationPreviewControl, IRelayCommand?>(
            nameof(OpenViewerCommand));
    public static readonly StyledProperty<double> PreviewSizeProperty =
        AvaloniaProperty.Register<GenerationPreviewControl, double>(
            nameof(PreviewSize),
            DefaultPreviewSize);

    public IRelayCommand? OpenViewerCommand
    {
        get => GetValue(OpenViewerCommandProperty);
        set => SetValue(OpenViewerCommandProperty, value);
    }
    public double PreviewSize
    {
        get => GetValue(PreviewSizeProperty);
        set => SetValue(PreviewSizeProperty, value);
    }

    private const int DragPreviewWidth = 256;
    private const double DefaultPreviewSize = 220d;

    private readonly GenerationPreviewExpansionController _previewExpansionController;
    private GalleryImageDragCandidate? _imageDragCandidate;

    public GenerationPreviewControl()
    {
        InitializeComponent();
        _previewExpansionController = new GenerationPreviewExpansionController(
            this,
            PreviewExpansionHost,
            PreviewShadow,
            PreviewImage,
            PreviewDragSource);
    }

    internal static string? GetImageDragPathOrDefault(GenerationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        string? imagePath = item.ImagePath;

        if (!item.ShowsGeneratedImage
            || string.IsNullOrWhiteSpace(imagePath)
            || !File.Exists(imagePath))
        {
            return null;
        }

        return imagePath;
    }

    internal static string? GetImageDragPreviewPathOrDefault(GenerationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        string? thumbnailPath = item.ThumbnailPath;

        if (!string.IsNullOrWhiteSpace(thumbnailPath) && File.Exists(thumbnailPath))
        {
            return thumbnailPath;
        }

        return GetImageDragPathOrDefault(item);
    }

    internal static DataTransfer CreateImageFileDataTransfer(IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return GalleryImageDragData.Create(file);
    }

    private void OnPreviewDragSourcePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = sender;

        if (DataContext is not GenerationItemViewModel item)
        {
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(this);
        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        string? imagePath = GetImageDragPathOrDefault(item);
        if (imagePath is null)
        {
            return;
        }

        e.Pointer.Capture(PreviewDragSource);
        string previewPath = GetImageDragPreviewPathOrDefault(item) ?? imagePath;
        _imageDragCandidate = new GalleryImageDragCandidate(
            e,
            pointerPoint.Position,
            item,
            imagePath,
            previewPath);
    }

    private async void OnPreviewDragSourcePointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;

        GalleryImageDragCandidate? dragCandidate = _imageDragCandidate;
        if (dragCandidate is null)
        {
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(this);
        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            _imageDragCandidate = null;
            return;
        }

        if (!PointerDragThreshold.IsReached(dragCandidate.Origin, pointerPoint.Position))
        {
            return;
        }

        _imageDragCandidate = null;
        e.Handled = true;

        try
        {
            await StartImageFileDragAsync(
                dragCandidate.PointerPressedEventArgs,
                dragCandidate.ImagePath,
                dragCandidate.PreviewPath);
        }
        finally
        {
            e.Pointer.Capture(null);
        }
    }

    private void OnPreviewDragSourcePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _ = sender;

        GalleryImageDragCandidate? dragCandidate = _imageDragCandidate;

        _imageDragCandidate = null;
        e.Pointer.Capture(null);

        if (dragCandidate is not null)
        {
            ExecuteOpenViewer(dragCandidate.Item);
            e.Handled = true;
        }
    }

    private void OnPreviewDragSourcePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _ = sender;
        _ = e;

        _imageDragCandidate = null;
    }

    private async Task StartImageFileDragAsync(
        PointerPressedEventArgs e,
        string imagePath,
        string previewPath)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        IStorageFile? file = await topLevel.StorageProvider.TryGetFileFromPathAsync(imagePath);
        if (file is null)
        {
            return;
        }

        DataTransfer dataTransfer = CreateImageFileDataTransfer(file);
        using GenerationDragPreviewWindow? previewWindow =
            CreateDragPreviewWindowOrDefault(previewPath);
        previewWindow?.Start(topLevel as Window);

        await DragDrop.DoDragDropAsync(e, dataTransfer, DragDropEffects.Copy);
    }

    private static GenerationDragPreviewWindow? CreateDragPreviewWindowOrDefault(
        string previewPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        Bitmap? bitmap = CreateDragPreviewBitmapOrDefault(previewPath);
        if (bitmap is null)
        {
            return null;
        }

        return new GenerationDragPreviewWindow(bitmap);
    }

    private static Bitmap? CreateDragPreviewBitmapOrDefault(string previewPath)
    {
        try
        {
            using FileStream stream = File.OpenRead(previewPath);

            return Bitmap.DecodeToWidth(
                stream,
                DragPreviewWidth,
                BitmapInterpolationMode.HighQuality);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            return null;
        }
    }

    private void ExecuteOpenViewer(GenerationItemViewModel item)
    {
        IRelayCommand? openViewerCommand = OpenViewerCommand;

        if (openViewerCommand?.CanExecute(item) == true)
        {
            openViewerCommand.Execute(item);
        }
    }

    private sealed record GalleryImageDragCandidate(
        PointerPressedEventArgs PointerPressedEventArgs,
        Point Origin,
        GenerationItemViewModel Item,
        string ImagePath,
        string PreviewPath);
}
