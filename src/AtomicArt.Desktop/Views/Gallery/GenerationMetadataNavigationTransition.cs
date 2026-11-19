using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Media.Imaging;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Views.Gallery;

internal sealed class GenerationMetadataNavigationTransition(
    GenerationMetadataLayout layout,
    Control page,
    Control currentBody,
    GenerationPreviewControl preview,
    Image previousPage,
    UiAnimationScheduler animation)
{
    private const int HeightDurationMilliseconds = 160;
    private const int FadeOutDurationMilliseconds = 90;
    private const int FadeInDurationMilliseconds = 120;
    private static readonly TimeSpan PreviewWaitLimit = TimeSpan.FromMilliseconds(200d);

    private TimeSpan? _previewWaitStarted;
    private bool _waitingForPreview;
    private bool _previewFrameRequested;

    internal void Start()
    {
        double startHeight = layout.Bounds.Height;
        if (!page.IsEffectivelyVisible || startHeight <= 0d || TopLevel.GetTopLevel(page) is not { } topLevel)
        {
            return;
        }

        bool transitionInProgress = animation.HasActiveAnimations || previousPage.Source is not null;
        RenderTargetBitmap? snapshot = transitionInProgress ? null : CapturePage(topLevel);
        animation.Cancel(layout);
        previousPage.Width = page.Bounds.Width;
        previousPage.Height = page.Bounds.Height;
        SetSnapshot(snapshot);
        currentBody.Opacity = snapshot is null ? 1d : 0d;

        _ = animation.AnimateValueAsync(
            layout,
            0d,
            1d,
            HeightDurationMilliseconds,
            0,
            MotionEasing.EaseOut,
            progress => layout.SetHeightTransition(startHeight, progress));

        _waitingForPreview = snapshot is not null;
        _previewWaitStarted = null;
        if (_waitingForPreview)
        {
            RequestPreviewFrame();
        }
    }

    internal void Reset()
    {
        _waitingForPreview = false;
        animation.Cancel(layout);
        layout.SetHeightTransition(layout.Bounds.Height, 1d);
        SetSnapshot(null);
        currentBody.Opacity = 1d;
    }

    private RenderTargetBitmap? CapturePage(TopLevel topLevel)
    {
        try
        {
            return VisualSnapshotRenderer.Capture(topLevel, page);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(
                page,
                "Failed to capture generation properties: {Exception}",
                ex);
            return null;
        }
    }

    private void SetSnapshot(RenderTargetBitmap? snapshot)
    {
        RenderTargetBitmap? previousSnapshot = previousPage.Source as RenderTargetBitmap;
        previousPage.Source = snapshot;
        previousPage.Opacity = snapshot is null ? 0d : 1d;
        previousSnapshot?.Dispose();
    }

    private void RequestPreviewFrame()
    {
        if (_previewFrameRequested)
        {
            return;
        }

        _previewFrameRequested = true;
        animation.RequestAnimationFrame(OnPreviewFrame);
    }

    private void OnPreviewFrame(TimeSpan now)
    {
        _previewFrameRequested = false;
        if (!_waitingForPreview)
        {
            return;
        }

        _previewWaitStarted ??= now;
        if (preview.IsPreviewLoadPending && now - _previewWaitStarted < PreviewWaitLimit)
        {
            RequestPreviewFrame();
            return;
        }

        _waitingForPreview = false;
        _ = animation.AnimateValueAsync(
            layout,
            1d,
            0d,
            FadeOutDurationMilliseconds,
            0,
            MotionEasing.Linear,
            opacity => previousPage.Opacity = opacity,
            () => SetSnapshot(null));
        _ = animation.AnimateValueAsync(
            layout,
            0d,
            1d,
            FadeInDurationMilliseconds,
            0,
            MotionEasing.EaseOut,
            opacity => currentBody.Opacity = opacity);
    }
}
