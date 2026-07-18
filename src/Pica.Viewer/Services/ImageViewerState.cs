namespace Pica.Viewer.Services;

public sealed class ImageViewerState
{
    public bool IsFilteringEnabled { get; init; } = ViewerSettingsDefaults.FilteringEnabled;
    public int MovementSpeed { get; init; } = ViewerSettingsDefaults.MovementSpeed;
    public int ZoomSpeed { get; init; } = ViewerSettingsDefaults.ZoomSpeed;
    public bool ExpandOnDoubleClick { get; init; } = ViewerSettingsDefaults.ExpandOnDoubleClick;
    public bool IsFastLoadingEnabled { get; init; } = ViewerSettingsDefaults.FastLoadingEnabled;
    public bool AllowFreeZoomOut { get; init; } = ViewerSettingsDefaults.AllowFreeZoomOut;
    public bool IsSmoothPanningEnabled { get; init; } = ViewerSettingsDefaults.SmoothPanningEnabled;
    public bool IsPanningInertiaEnabled { get; init; } = ViewerSettingsDefaults.PanningInertiaEnabled;
    public WindowResizeBehavior ResizeBehavior { get; init; } = ViewerSettingsDefaults.ResizeBehavior;
    public bool RememberWindowPlacement { get; init; } = ViewerSettingsDefaults.RememberWindowPlacement;
    public bool? IsWindowed { get; init; }
    public int? WindowX { get; init; }
    public int? WindowY { get; init; }
    public double? WindowWidth { get; init; }
    public double? WindowHeight { get; init; }
}
