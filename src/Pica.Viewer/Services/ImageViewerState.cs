namespace Pica.Viewer.Services;

public sealed class ImageViewerState
{
    public bool IsFilteringEnabled { get; set; } = ViewerSettingsDefaults.FilteringEnabled;
    public int MovementSpeed { get; set; } = ViewerSettingsDefaults.MovementSpeed;
    public int ZoomSpeed { get; set; } = ViewerSettingsDefaults.ZoomSpeed;
    public bool ExpandOnDoubleClick { get; set; } = ViewerSettingsDefaults.ExpandOnDoubleClick;
    public bool IsFastLoadingEnabled { get; set; } = ViewerSettingsDefaults.FastLoadingEnabled;
    public bool AllowFreeZoomOut { get; set; } = ViewerSettingsDefaults.AllowFreeZoomOut;
    public bool IsSmoothPanningEnabled { get; set; } = ViewerSettingsDefaults.SmoothPanningEnabled;
    public bool IsPanningInertiaEnabled { get; set; } = ViewerSettingsDefaults.PanningInertiaEnabled;
    public WindowResizeBehavior ResizeBehavior { get; set; } = ViewerSettingsDefaults.ResizeBehavior;
    public bool RememberWindowPlacement { get; set; } = ViewerSettingsDefaults.RememberWindowPlacement;
    public bool? IsWindowed { get; set; }
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }

    internal ImageViewerState CreateCopy()
    {
        return (ImageViewerState)MemberwiseClone();
    }

    internal ImageViewerState CreateSnapshot(
        bool isWindowed,
        int? windowX,
        int? windowY,
        double? windowWidth,
        double? windowHeight)
    {
        ImageViewerState snapshot = CreateCopy();
        snapshot.IsWindowed = RememberWindowPlacement && isWindowed;
        snapshot.WindowX = RememberWindowPlacement ? windowX : null;
        snapshot.WindowY = RememberWindowPlacement ? windowY : null;
        snapshot.WindowWidth = RememberWindowPlacement ? windowWidth : null;
        snapshot.WindowHeight = RememberWindowPlacement ? windowHeight : null;

        return snapshot;
    }
}
