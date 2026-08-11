using Avalonia;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryLayoutService
{
    public const double CardWidth = 236d;
    public const double CardHeight = 338d;
    public const double CardGap = 0d;
    public const double CardTopPadding = 20d;
    public const double OverscanPixels = 360d;

    private const int MinimumColumnCount = 1;
    private const double MinimumMeasuredViewportSize = 1d;
    private const double ViewportHorizontalPadding = 32d;
    private const double ScrollOffsetTolerance = 0.5d;
    private static readonly AttachedProperty<bool> HiddenByLayoutProperty =
        AvaloniaProperty.RegisterAttached<GalleryLayoutService, Control, bool>("HiddenByLayout");

    private VirtualizedRange? _renderedRange;

    public void RenderCards(
        GalleryOperationCoordinator context,
        IReadOnlySet<Guid>? hiddenItemIds = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.EnsureSceneAttached();

        context.HiddenItemIds.Clear();
        if (hiddenItemIds is not null)
        {
            foreach (Guid id in hiddenItemIds)
            {
                context.HiddenItemIds.Add(id);
            }
        }

        _renderedRange = null;
        RefreshGalleryVirtualization(context);
        context.NotifyStateChanged();
    }

    public void RefreshGalleryVirtualization(GalleryOperationCoordinator context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.EnsureSceneAttached();
        SynchronizeCardControlIdsCore(context);
        IList<object> items = context.Items;

        if (items.Count == 0)
        {
            RecycleAllControls(context);
            context.GalleryPanel.Width = 0d;
            context.GalleryPanel.Height = OverscanPixels;
            _renderedRange = null;
            return;
        }

        int columns = CalculateColumnCount(GetViewportWidth(context.ScrollViewer));
        bool layoutChanged = _renderedRange is not VirtualizedRange renderedRange
            || renderedRange.Columns != columns
            || renderedRange.ItemCount != items.Count;

        if (layoutChanged)
        {
            UpdatePanelSize(context, columns, items.Count);
            ClampScrollOffset(context);
        }

        (int start, int end) = GetVisibleIndexRange(context, columns, items.Count);
        VirtualizedRange currentRange = new(start, end, columns, items.Count);

        int expectedControlCount = end - start;
        if ((_renderedRange == currentRange)
            && (context.CardControls.Count == expectedControlCount))
        {
            return;
        }

        HashSet<Guid> desiredIds = [];

        for (int i = start; i < end; i++)
        {
            desiredIds.Add(context.GetItemId(items[i]));
        }

        RemoveInvisibleControls(context, desiredIds);
        EnsureVisibleControls(context, items, start, end, columns);
        _renderedRange = currentRange;
    }

    public Dictionary<Guid, Rect> TakeSnapshot(GalleryOperationCoordinator context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return TakeSnapshotExcept(context, new HashSet<Guid>());
    }

    public Dictionary<Guid, Rect> TakeSnapshotExcept(
        GalleryOperationCoordinator context,
        IReadOnlySet<Guid> excludedIds)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(excludedIds);

        Dictionary<Guid, Rect> result = [];

        foreach (KeyValuePair<Guid, Control> pair in context.CardControls)
        {
            if (excludedIds.Contains(pair.Key))
            {
                continue;
            }

            if (TryGetOverlayRect(pair.Value, context.OverlayCanvas, out Rect rect))
            {
                result[pair.Key] = rect;
            }
        }

        return result;
    }

    public void SynchronizeCardControlIds(GalleryOperationCoordinator context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.EnsureSceneAttached();
        SynchronizeCardControlIdsCore(context);
    }

    public int? FindItemIndex(
        GalleryOperationCoordinator context,
        Guid itemId)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.EnsureSceneAttached();
        IList<object> items = context.Items;

        for (int index = 0; index < items.Count; index++)
        {
            if (context.GetItemId(items[index]) == itemId)
            {
                return index;
            }
        }

        return null;
    }

    public void ScrollItemIntoView(
        GalleryOperationCoordinator context,
        int itemIndex)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.EnsureSceneAttached();
        RefreshGalleryVirtualization(context);
        int columns = CalculateColumnCount(GetViewportWidth(context.ScrollViewer));
        Rect itemRect = GetLogicalCardRect(itemIndex, columns);
        double viewportHeight = GetViewportHeight(context.ScrollViewer);
        double targetOffsetY = itemRect.Center.Y - (viewportHeight / 2d);
        double maximumOffsetY = Math.Max(
            0d,
            context.GalleryPanel.Height - viewportHeight);
        Vector currentOffset = context.ScrollViewer.Offset;
        context.ScrollViewer.Offset = new Vector(
            currentOffset.X,
            Math.Clamp(targetOffsetY, 0d, maximumOffsetY));
        RefreshGalleryVirtualization(context);
    }

    public Dictionary<Guid, Rect> TakeOverlaySnapshots(
        Canvas overlayCanvas,
        Dictionary<Guid, Control> overlays)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);
        ArgumentNullException.ThrowIfNull(overlays);

        Dictionary<Guid, Rect> result = [];

        foreach (KeyValuePair<Guid, Control> pair in overlays)
        {
            if (TryGetOverlayRect(pair.Value, overlayCanvas, out Rect rect))
            {
                result[pair.Key] = rect;
            }
        }

        return result;
    }

    public bool TryGetOverlayRect(Control control, Canvas overlayCanvas, out Rect rect)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        Matrix? matrix = control.TransformToVisual(overlayCanvas);
        if (matrix is null)
        {
            return TryGetCanvasRect(control, out rect);
        }

        Rect localBounds = new(control.Bounds.Size);
        rect = localBounds.TransformToAABB(matrix.Value);
        if ((rect.Width <= 0d) || (rect.Height <= 0d))
        {
            return TryGetCanvasRect(control, out rect);
        }

        return rect is { Width: > 0d, Height: > 0d };
    }

    public bool TryGetCardSurface(
        Control control,
        Canvas overlayCanvas,
        out Rect rect,
        out CornerRadius cornerRadius)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        Control surface = ResolveCardSurface(control);

        if (!TryGetOverlayRect(surface, overlayCanvas, out rect))
        {
            cornerRadius = default;
            return false;
        }

        cornerRadius = surface is Border border
            ? border.CornerRadius
            : default;

        return true;
    }

    public int CalculateColumnCount(double viewportWidth)
    {
        double pitchX = CardWidth + CardGap;

        return Math.Max(MinimumColumnCount, (int)Math.Floor((viewportWidth + CardGap) / Math.Max(MinimumMeasuredViewportSize, pitchX)));
    }

    public Rect GetLogicalCardRect(int index, int columns)
    {
        int safeColumns = Math.Max(MinimumColumnCount, columns);
        int column = index % safeColumns;
        int row = index / safeColumns;

        return new Rect(
            column * (CardWidth + CardGap),
            CardTopPadding + (row * (CardHeight + CardGap)),
            CardWidth,
            CardHeight);
    }

    public (int Start, int End) CalculateVisibleIndexRange(
        int itemCount,
        int columns,
        double scrollOffsetY,
        double viewportHeight)
    {
        double pitchY = CardHeight + CardGap;
        double top = Math.Max(0d, scrollOffsetY - OverscanPixels);
        double bottom = scrollOffsetY + viewportHeight + OverscanPixels;
        int startRow = Math.Max(0, (int)Math.Floor((top - CardTopPadding) / Math.Max(MinimumMeasuredViewportSize, pitchY)));
        int endRow = Math.Max(startRow, (int)Math.Ceiling((bottom - CardTopPadding) / Math.Max(MinimumMeasuredViewportSize, pitchY)));
        int start = Math.Clamp(startRow * columns, 0, itemCount);
        int end = Math.Clamp((endRow + 1) * columns, start, itemCount);

        return (start, end);
    }

    private static void SynchronizeCardControlIdsCore(GalleryOperationCoordinator context)
    {
        if (context.CardControls.Count == 0)
        {
            return;
        }

        List<(Guid PreviousId, Guid CurrentId, Control Control)>? remaps = null;

        foreach (KeyValuePair<Guid, Control> pair in context.CardControls)
        {
            object? item = pair.Value.DataContext;
            if (item is null)
            {
                continue;
            }

            Guid currentId = context.GetItemId(item);
            if ((currentId == pair.Key) || context.CardControls.ContainsKey(currentId))
            {
                continue;
            }

            remaps ??= new List<(Guid PreviousId, Guid CurrentId, Control Control)>();
            remaps.Add((pair.Key, currentId, pair.Value));
        }

        if (remaps is null)
        {
            return;
        }

        foreach ((Guid previousId, Guid currentId, Control control) in remaps)
        {
            context.CardControls.Remove(previousId);
            context.CardControls[currentId] = control;
            if (context.HiddenItemIds.Remove(previousId))
            {
                context.HiddenItemIds.Add(currentId);
            }
        }
    }

    private static Control ResolveCardSurface(Control control)
    {
        if (control is IGalleryCardSurfaceProvider surfaceProvider)
        {
            return surfaceProvider.CardSurface;
        }

        return control is ContentControl { Content: Control content }
            ? content
            : control;
    }

    private static void RecycleAllControls(GalleryOperationCoordinator context)
    {
        foreach (Control control in context.CardControls.Values)
        {
            RecycleControl(context, control);
        }

        context.CardControls.Clear();
    }

    private static double GetViewportHeight(ScrollViewer scrollViewer)
    {
        if (scrollViewer.Viewport.Height > MinimumMeasuredViewportSize)
        {
            return scrollViewer.Viewport.Height;
        }

        return Math.Max(OverscanPixels, scrollViewer.Bounds.Height);
    }

    private static double GetViewportWidth(ScrollViewer scrollViewer)
    {
        double width = scrollViewer.Viewport.Width;
        if (width <= MinimumMeasuredViewportSize)
        {
            width = scrollViewer.Bounds.Width;
        }

        return Math.Max(CardWidth, width - ViewportHorizontalPadding);
    }

    private static bool TryGetCanvasRect(Control control, out Rect rect)
    {
        rect = new Rect(
            Canvas.GetLeft(control),
            Canvas.GetTop(control),
            control.Bounds.Width > 0d ? control.Bounds.Width : control.Width,
            control.Bounds.Height > 0d ? control.Bounds.Height : control.Height);

        return rect is { Width: > 0d, Height: > 0d };
    }

    private static void PositionControl(Control control, Rect rect, bool hidden)
    {
        bool wasHiddenByLayout = control.GetValue(HiddenByLayoutProperty);

        control.Width = rect.Width;
        control.Height = rect.Height;
        control.Margin = new Thickness(0d);
        Canvas.SetLeft(control, rect.Left);
        Canvas.SetTop(control, rect.Top);

        if (hidden)
        {
            control.SetValue(HiddenByLayoutProperty, true);
            control.Opacity = 0d;
            return;
        }

        control.SetValue(HiddenByLayoutProperty, false);
        control.Opacity = wasHiddenByLayout ? 1d : Math.Clamp(control.Opacity, 0d, 1d);
    }

    private void UpdatePanelSize(
        GalleryOperationCoordinator context,
        int columns,
        int itemCount)
    {
        double pitchX = CardWidth + CardGap;
        double pitchY = CardHeight + CardGap;
        int usedColumns = Math.Min(columns, itemCount);
        int rows = (int)Math.Ceiling(itemCount / (double)columns);

        context.GalleryPanel.Width = (usedColumns * pitchX) - CardGap;
        context.GalleryPanel.Height = Math.Max(OverscanPixels, CardTopPadding + (rows * pitchY) - CardGap);
    }

    private void ClampScrollOffset(GalleryOperationCoordinator context)
    {
        double viewportHeight = GetViewportHeight(context.ScrollViewer);
        double maxOffsetY = Math.Max(0d, context.GalleryPanel.Height - viewportHeight);
        Vector offset = context.ScrollViewer.Offset;
        double clampedOffsetY = Math.Clamp(offset.Y, 0d, maxOffsetY);

        if (Math.Abs(clampedOffsetY - offset.Y) < ScrollOffsetTolerance)
        {
            return;
        }

        context.ScrollViewer.Offset = new Vector(offset.X, clampedOffsetY);
    }

    private (int Start, int End) GetVisibleIndexRange(
        GalleryOperationCoordinator context,
        int columns,
        int itemCount)
    {
        return CalculateVisibleIndexRange(
            itemCount,
            columns,
            context.ScrollViewer.Offset.Y,
            GetViewportHeight(context.ScrollViewer));
    }

    private void RemoveInvisibleControls(
        GalleryOperationCoordinator context,
        HashSet<Guid> desiredIds)
    {
        foreach (Guid id in context.CardControls.Keys.ToArray())
        {
            if (desiredIds.Contains(id))
            {
                continue;
            }

            Control control = context.CardControls[id];

            context.CardControls.Remove(id);
            RecycleControl(context, control);
        }
    }

    private void EnsureVisibleControls(
        GalleryOperationCoordinator context,
        IList<object> items,
        int start,
        int end,
        int columns)
    {
        for (int i = start; i < end; i++)
        {
            object item = items[i];
            Guid id = context.GetItemId(item);
            if (!context.CardControls.TryGetValue(id, out Control? control))
            {
                control = context.CreateControl(item);
                control.IsVisible = true;
                context.CardControls[id] = control;

                if (!context.GalleryPanel.Children.Contains(control))
                {
                    context.GalleryPanel.Children.Add(control);
                }
            }

            PositionControl(control, GetLogicalCardRect(i, columns), context.HiddenItemIds.Contains(id));
        }
    }

    private static void RecycleControl(
        GalleryOperationCoordinator context,
        Control control)
    {
        if (!context.CanRetainRecycledControl(control))
        {
            context.GalleryPanel.Children.Remove(control);
        }

        context.RecycleControl(control);
    }

    private readonly record struct VirtualizedRange(
        int Start,
        int End,
        int Columns,
        int ItemCount);
}
