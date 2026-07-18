using Avalonia;

using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

internal sealed class ViewerSettingsState
{
    public bool IsFilteringEnabled { get; set; }
    public int MovementSpeed { get; set; }
    public int ZoomSpeed { get; set; }
    public bool ExpandOnDoubleClick { get; set; }
    public bool IsFastLoadingEnabled { get; set; }
    public bool AllowFreeZoomOut { get; set; }
    public bool IsSmoothPanningEnabled { get; set; }
    public bool IsPanningInertiaEnabled { get; set; }
    public WindowResizeBehavior ResizeBehavior { get; set; }
    public bool RememberWindowPlacement { get; set; }

    private ViewerSettingsState(ImageViewerState state)
    {
        IsFilteringEnabled = state.IsFilteringEnabled;
        MovementSpeed = state.MovementSpeed;
        ZoomSpeed = state.ZoomSpeed;
        ExpandOnDoubleClick = state.ExpandOnDoubleClick;
        IsFastLoadingEnabled = state.IsFastLoadingEnabled;
        AllowFreeZoomOut = state.AllowFreeZoomOut;
        IsSmoothPanningEnabled = state.IsSmoothPanningEnabled;
        IsPanningInertiaEnabled = state.IsPanningInertiaEnabled;
        ResizeBehavior = state.ResizeBehavior;
        RememberWindowPlacement = state.RememberWindowPlacement;
    }

    public static ViewerSettingsState Create(ImageViewerState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new ViewerSettingsState(state);
    }

    public ImageViewerState CreateImageViewerState(
        bool isWindowedMode,
        PixelPoint? windowedPosition,
        Size? windowedClientSize)
    {
        return new ImageViewerState
        {
            IsFilteringEnabled = IsFilteringEnabled,
            MovementSpeed = MovementSpeed,
            ZoomSpeed = ZoomSpeed,
            ExpandOnDoubleClick = ExpandOnDoubleClick,
            IsFastLoadingEnabled = IsFastLoadingEnabled,
            AllowFreeZoomOut = AllowFreeZoomOut,
            IsSmoothPanningEnabled = IsSmoothPanningEnabled,
            IsPanningInertiaEnabled = IsPanningInertiaEnabled,
            ResizeBehavior = ResizeBehavior,
            RememberWindowPlacement = RememberWindowPlacement,
            IsWindowed = RememberWindowPlacement && isWindowedMode,
            WindowX = RememberWindowPlacement ? windowedPosition?.X : null,
            WindowY = RememberWindowPlacement ? windowedPosition?.Y : null,
            WindowWidth = RememberWindowPlacement ? windowedClientSize?.Width : null,
            WindowHeight = RememberWindowPlacement ? windowedClientSize?.Height : null
        };
    }
}
