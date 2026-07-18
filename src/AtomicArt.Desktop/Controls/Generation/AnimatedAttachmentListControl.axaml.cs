using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.GalleryAnimation;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Controls.Generation;

public partial class AnimatedAttachmentListControl : UserControl
{
    public static readonly StyledProperty<IEnumerable<AttachedImageViewModel>?> ItemsProperty =
        AvaloniaProperty.Register<AnimatedAttachmentListControl, IEnumerable<AttachedImageViewModel>?>(
            nameof(Items));
    public static readonly StyledProperty<ICommand?> RemoveAttachmentCommandProperty =
        AvaloniaProperty.Register<AnimatedAttachmentListControl, ICommand?>(
            nameof(RemoveAttachmentCommand));
    public static readonly StyledProperty<ICommand?> ReorderAttachmentCommandProperty =
        AvaloniaProperty.Register<AnimatedAttachmentListControl, ICommand?>(
            nameof(ReorderAttachmentCommand));
    public static readonly StyledProperty<ICommand?> OpenAttachmentCommandProperty =
        AvaloniaProperty.Register<AnimatedAttachmentListControl, ICommand?>(
            nameof(OpenAttachmentCommand));

    private const double DefaultPreviewSize = 56d;
    private const double DefaultPreviewGap = 8d;
    private const double MovementTolerance = 0.5d;
    private const int SpawnDurationMilliseconds = 280;
    private const int MoveDurationMilliseconds = 260;
    private const int RemoveDurationMilliseconds = 300;
    private const int ImageRevealDurationMilliseconds = 520;
    private const int SpawnOrderDelayMilliseconds = 24;

    private readonly CollectionChangedSubscription _itemsSubscription;
    private readonly Dictionary<Guid, AttachmentVisualEntry> _entries = [];
    private readonly List<AttachmentVisualEntry> _removingEntries = [];
    private GalleryAnimationScheduler? _animationScheduler;
    private AttachmentDragCandidate? _dragCandidate;
    private AttachmentDragState? _dragState;
    private bool _isAttached;

    public IEnumerable<AttachedImageViewModel>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }
    public ICommand? RemoveAttachmentCommand
    {
        get => GetValue(RemoveAttachmentCommandProperty);
        set => SetValue(RemoveAttachmentCommandProperty, value);
    }
    public ICommand? ReorderAttachmentCommand
    {
        get => GetValue(ReorderAttachmentCommandProperty);
        set => SetValue(ReorderAttachmentCommandProperty, value);
    }
    public ICommand? OpenAttachmentCommand
    {
        get => GetValue(OpenAttachmentCommandProperty);
        set => SetValue(OpenAttachmentCommandProperty, value);
    }

    public AnimatedAttachmentListControl()
    {
        _itemsSubscription = new CollectionChangedSubscription(OnItemsCollectionChanged);
        InitializeComponent();
        UpdatePanelSize(0);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _isAttached = true;
        EnsureAnimationScheduler();
        _itemsSubscription.ReplaceSource(Items);
        SynchronizeItems(AttachmentMutationMode.Instant);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        CancelDrag();
        _itemsSubscription.Clear();
        CancelAllAnimations();

        foreach (AttachmentVisualEntry entry in _entries.Values.Concat(_removingEntries))
        {
            entry.DisposeVisual();
        }

        AttachmentPanel.Children.Clear();
        AttachmentOverlayCanvas.Children.Clear();
        _entries.Clear();
        _removingEntries.Clear();

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsProperty)
        {
            HandleItemsChanged();
            return;
        }

        if (change.Property == RemoveAttachmentCommandProperty)
        {
            UpdateRemoveCommands();
        }
    }

    internal static int CalculateTargetIndex(
        double draggedCenterX,
        int itemCount,
        double slotWidth)
    {
        if (itemCount <= 0)
        {
            return 0;
        }

        int targetIndex = (int)Math.Floor(draggedCenterX / Math.Max(1d, slotWidth));

        return Math.Clamp(targetIndex, 0, itemCount - 1);
    }

    private static Bitmap CreateBitmap(AttachedImageViewModel item)
    {
        using MemoryStream stream = new(item.Content);

        return new Bitmap(stream);
    }

    internal static List<MotionFrame> CreateRemoveFrames(Guid itemId)
    {
        AttachmentMotion motion = CreateAttachmentMotion(itemId, 18d, 16);
        List<MotionFrame> frames =
        [
            MotionFrame.Identity,
            new(motion.X * 0.36d, motion.Y * 0.36d, 0.97d, motion.Rotate * 0.35d, 0.72d),
            new(motion.X, motion.Y, 0.88d, motion.Rotate, 0d)
        ];

        return frames;
    }

    internal static List<MotionFrame> CreateSpawnFrames(Guid itemId)
    {
        AttachmentMotion motion = CreateAttachmentMotion(itemId, 8d, 10);
        List<MotionFrame> frames =
        [
            new(-motion.X, -motion.Y, 0.94d, -motion.Rotate * 0.35d, 0d),
            MotionFrame.Identity
        ];

        return frames;
    }

    private static AttachmentMotion CreateAttachmentMotion(
        Guid itemId,
        double baseDistance,
        int distanceVariance)
    {
        byte[] bytes = itemId.ToByteArray();
        double angle = Math.PI * 2d * bytes[0] / byte.MaxValue;
        double distance = baseDistance + (bytes[1] % distanceVariance);
        double x = Math.Cos(angle) * distance;
        double y = Math.Sin(angle) * distance;
        double rotateSign = x >= 0d ? 1d : -1d;
        double rotate = rotateSign * (5d + (bytes[2] % 7));

        return new AttachmentMotion(x, y, rotate);
    }

    private static double GetCurrentX(Control control)
    {
        double left = Canvas.GetLeft(control);
        if (double.IsNaN(left))
        {
            left = 0d;
        }

        if (control.RenderTransform is not TransformGroup transformGroup)
        {
            return left;
        }

        TranslateTransform? translate = transformGroup.Children
            .OfType<TranslateTransform>()
            .SingleOrDefault();

        return left + (translate?.X ?? 0d);
    }

    private static IReadOnlyList<Control> GetEntryAnimationControls(
        AttachmentVisualEntry entry)
    {
        return
        [
            entry.Control,
            entry.Image
        ];
    }

    private void HandleItemsChanged()
    {
        if (_isAttached)
        {
            _itemsSubscription.ReplaceSource(Items);
            SynchronizeItems(AttachmentMutationMode.Animated);
            return;
        }

        _itemsSubscription.Clear();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;

        bool shouldScrollToEnd = ShouldScrollToEndAfterCollectionChanged(e);
        SynchronizeItems(AttachmentMutationMode.Animated);

        if (shouldScrollToEnd)
        {
            ScrollToEnd();
        }
    }

    private void SynchronizeItems(AttachmentMutationMode mode)
    {
        IReadOnlyList<AttachedImageViewModel> nextItems = Items?.ToList()
            ?? [];
        HashSet<Guid> nextIds = nextItems
            .Select(item => item.Id)
            .ToHashSet();
        double slotWidth = GetSlotWidth();

        RemoveMissingEntries(nextIds, mode);
        AddOrMoveEntries(nextItems, slotWidth, mode);
        UpdatePanelSize(nextItems.Count);
    }

    private void RemoveMissingEntries(
        IReadOnlySet<Guid> nextIds,
        AttachmentMutationMode mode)
    {
        List<AttachmentVisualEntry> removedEntries = _entries
            .Where(pair => !nextIds.Contains(pair.Key))
            .Select(pair => pair.Value)
            .ToList();

        foreach (AttachmentVisualEntry entry in removedEntries)
        {
            _entries.Remove(entry.Item.Id);
            RemoveEntry(entry, mode);
        }
    }

    private void AddOrMoveEntries(
        IReadOnlyList<AttachedImageViewModel> nextItems,
        double slotWidth,
        AttachmentMutationMode mode)
    {
        for (int index = 0; index < nextItems.Count; index++)
        {
            AttachedImageViewModel item = nextItems[index];
            double targetX = index * slotWidth;

            if (_entries.TryGetValue(item.Id, out AttachmentVisualEntry? entry))
            {
                MoveEntry(entry, targetX, mode);
                continue;
            }

            AddEntry(item, targetX, mode, index);
        }
    }

    private void AddEntry(
        AttachedImageViewModel item,
        double targetX,
        AttachmentMutationMode mode,
        int order)
    {
        AttachmentVisualEntry entry = CreateEntry(item);
        _entries[item.Id] = entry;
        AttachmentPanel.Children.Add(entry.Control);
        Canvas.SetLeft(entry.Control, targetX);
        Canvas.SetTop(entry.Control, 0d);

        if (mode == AttachmentMutationMode.Instant || _animationScheduler is null)
        {
            MotionFrameApplier.Apply(entry.Control, MotionFrame.Identity);
            return;
        }

        _animationScheduler.Cancel([entry.Control]);
        _ = _animationScheduler.AnimateAsync(
            entry.Control,
            CreateSpawnFrames(item.Id),
            SpawnDurationMilliseconds,
            order * SpawnOrderDelayMilliseconds,
            MotionEasing.EaseOut);
    }

    private void MoveEntry(
        AttachmentVisualEntry entry,
        double targetX,
        AttachmentMutationMode mode)
    {
        double currentX = GetCurrentX(entry.Control);
        double offsetX = currentX - targetX;
        Canvas.SetLeft(entry.Control, targetX);
        Canvas.SetTop(entry.Control, 0d);

        if (mode == AttachmentMutationMode.Instant
            || _animationScheduler is null
            || Math.Abs(offsetX) < MovementTolerance)
        {
            MotionFrameApplier.Apply(entry.Control, MotionFrame.Identity);
            return;
        }

        _animationScheduler.Cancel([entry.Control]);
        _ = _animationScheduler.AnimateAsync(
            entry.Control,
            new List<MotionFrame>
            {
                new(offsetX, 0d, 1d, 0d, 1d),
                MotionFrame.Identity
            },
            MoveDurationMilliseconds,
            0,
            MotionEasing.EaseRail);
    }

    private void RemoveEntry(
        AttachmentVisualEntry entry,
        AttachmentMutationMode mode)
    {
        entry.Unsubscribe();
        double currentX = GetCurrentX(entry.Control);
        double overlayX = currentX - AttachmentScrollViewer.Offset.X;
        AttachmentPanel.Children.Remove(entry.Control);

        if (mode == AttachmentMutationMode.Instant
            || _animationScheduler is null)
        {
            entry.DisposeVisual();
            return;
        }

        AttachmentOverlayCanvas.Children.Add(entry.Control);
        _removingEntries.Add(entry);
        Canvas.SetLeft(entry.Control, overlayX);
        Canvas.SetTop(entry.Control, 0d);
        MotionFrameApplier.Apply(entry.Control, MotionFrame.Identity);

        _animationScheduler.Cancel(GetEntryAnimationControls(entry));
        _ = _animationScheduler.AnimateAsync(
            entry.Control,
            CreateRemoveFrames(entry.Item.Id),
            RemoveDurationMilliseconds,
            0,
            MotionEasing.EaseMaterial,
            () => CompleteRemoveAnimation(entry));
    }

    private AttachmentVisualEntry CreateEntry(AttachedImageViewModel item)
    {
        Image image = new()
        {
            IsVisible = item.IsReady,
            Stretch = Stretch.UniformToFill
        };
        RenderOptions.SetBitmapInterpolationMode(
            image,
            BitmapInterpolationMode.MediumQuality);
        AttachmentPixelLoadingControl loadingIndicator = new();

        if (item.IsReady)
        {
            loadingIndicator.ShowCompleted();
        }

        Button removeButton = new()
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Content = UiStrings.DeleteGlyph,
            Command = RemoveAttachmentCommand,
            CommandParameter = item
        };
        removeButton.Classes.Add("remove-attachment");

        Grid content = new();
        content.Children.Add(image);
        content.Children.Add(loadingIndicator);
        content.Children.Add(removeButton);

        Border preview = new()
        {
            Child = content
        };
        preview.Classes.Add("attachment-preview");

        ContentControl host = new()
        {
            Width = GetSlotWidth(),
            Height = GetPreviewSize(),
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = preview
        };

        AttachmentVisualEntry entry = new(
            item,
            host,
            removeButton,
            image,
            loadingIndicator);

        if (item.IsReady)
        {
            entry.SetBitmap(CreateBitmap(item));
        }

        entry.Subscribe((sender, e) => OnAttachmentPropertyChanged(entry, sender, e));
        host.PointerPressed += (sender, e) => OnAttachmentPointerPressed(entry, sender, e);
        host.PointerMoved += OnAttachmentPointerMoved;
        host.PointerReleased += OnAttachmentPointerReleased;
        host.PointerCaptureLost += OnAttachmentPointerCaptureLost;

        return entry;
    }

    private async void OnAttachmentPropertyChanged(
        AttachmentVisualEntry entry,
        object? sender,
        PropertyChangedEventArgs e)
    {
        _ = sender;

        if (!string.Equals(
                e.PropertyName,
                nameof(AttachedImageViewModel.IsReady),
                StringComparison.Ordinal)
            || !entry.Item.IsReady)
        {
            return;
        }

        Bitmap bitmap = await Task.Run(() => CreateBitmap(entry.Item));

        if (!_entries.TryGetValue(entry.Item.Id, out AttachmentVisualEntry? activeEntry)
            || !ReferenceEquals(activeEntry, entry))
        {
            bitmap.Dispose();
            return;
        }

        entry.SetBitmap(bitmap);
        RevealReadyImage(entry);
        entry.LoadingIndicator.Complete();
    }

    private void OnAttachmentPointerPressed(
        AttachmentVisualEntry entry,
        object? sender,
        PointerPressedEventArgs e)
    {
        _ = sender;

        if (IsRemoveButtonEvent(e.Source))
        {
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(AttachmentPanel);
        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        int sourceIndex = GetItemIndex(entry.Item);
        if (sourceIndex < 0)
        {
            return;
        }

        e.Pointer.Capture(entry.Control);
        _dragCandidate = new AttachmentDragCandidate(
            entry,
            pointerPoint.Position,
            pointerPoint.Position.X - GetCurrentX(entry.Control),
            sourceIndex,
            sourceIndex);
        e.Handled = true;
    }

    private void OnAttachmentPointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;

        PointerPoint pointerPoint = e.GetCurrentPoint(AttachmentPanel);
        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            CancelDrag();
            return;
        }

        AttachmentDragState? dragState = _dragState;
        if (dragState is not null)
        {
            UpdateActiveDrag(dragState, pointerPoint);
            e.Handled = true;
            return;
        }

        AttachmentDragCandidate? dragCandidate = _dragCandidate;
        if (dragCandidate is null)
        {
            return;
        }

        if (!PointerDragThreshold.IsReached(dragCandidate.Origin, pointerPoint.Position))
        {
            e.Handled = true;
            return;
        }

        BeginDrag(dragCandidate);
        dragState = _dragState;
        if (dragState is not null)
        {
            UpdateActiveDrag(dragState, pointerPoint);
        }

        e.Handled = true;
    }

    private void UpdateActiveDrag(AttachmentDragState dragState, PointerPoint pointerPoint)
    {
        double slotWidth = GetSlotWidth();
        double draggedX = pointerPoint.Position.X - dragState.PointerOffsetX;
        int targetIndex = CalculateTargetIndex(
            draggedX + (slotWidth / 2d),
            _entries.Count,
            slotWidth);
        Canvas.SetLeft(dragState.Entry.Control, draggedX);
        Canvas.SetTop(dragState.Entry.Control, 0d);
        MotionFrameApplier.Apply(dragState.Entry.Control, MotionFrame.Identity);

        if (targetIndex != dragState.TargetIndex)
        {
            _dragState = dragState with
            {
                TargetIndex = targetIndex
            };
            AnimateDragTargets(dragState.Entry.Item, targetIndex);
        }
    }

    private void OnAttachmentPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _ = sender;

        if (_dragState is null && _dragCandidate is null)
        {
            return;
        }

        if (_dragState is not null)
        {
            CompleteDrag(AttachmentDragCompletion.Commit);
        }
        else
        {
            if (_dragCandidate is not { } dragCandidate)
            {
                return;
            }

            if (dragCandidate.Entry.Item.IsReady)
            {
                ExecuteOpenAttachmentCommand(dragCandidate.Entry.Item);
            }

            _dragCandidate = null;
        }

        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void ExecuteOpenAttachmentCommand(AttachedImageViewModel item)
    {
        ICommand? command = OpenAttachmentCommand;
        if (command?.CanExecute(item) == true)
        {
            command.Execute(item);
        }
    }

    private void OnAttachmentPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _ = sender;
        _ = e;

        CompleteDrag(AttachmentDragCompletion.Cancel);
    }

    private static bool IsRemoveButtonEvent(object? source)
    {
        if (source is Button)
        {
            return true;
        }

        if (source is not Visual visual)
        {
            return false;
        }

        return visual.GetVisualAncestors().OfType<Button>().Any();
    }

    private void BeginDrag(AttachmentDragCandidate dragCandidate)
    {
        double currentX = GetCurrentX(dragCandidate.Entry.Control);
        Canvas.SetLeft(dragCandidate.Entry.Control, currentX);
        Canvas.SetTop(dragCandidate.Entry.Control, 0d);
        MotionFrameApplier.Apply(dragCandidate.Entry.Control, MotionFrame.Identity);
        _animationScheduler?.Cancel([dragCandidate.Entry.Control]);
        dragCandidate.Entry.Control.ZIndex = 1000;
        _dragCandidate = null;
        _dragState = new AttachmentDragState(
            dragCandidate.Entry,
            dragCandidate.PointerOffsetX,
            dragCandidate.SourceIndex,
            dragCandidate.TargetIndex);
    }

    private void AnimateDragTargets(AttachedImageViewModel draggedItem, int targetIndex)
    {
        IReadOnlyList<AttachedImageViewModel> orderedItems = CreateDragOrder(draggedItem, targetIndex);

        for (int index = 0; index < orderedItems.Count; index++)
        {
            AttachedImageViewModel item = orderedItems[index];
            if (ReferenceEquals(item, draggedItem))
            {
                continue;
            }

            if (_entries.TryGetValue(item.Id, out AttachmentVisualEntry? entry))
            {
                MoveEntry(entry, index * GetSlotWidth(), AttachmentMutationMode.Animated);
            }
        }
    }

    private IReadOnlyList<AttachedImageViewModel> CreateDragOrder(
        AttachedImageViewModel draggedItem,
        int targetIndex)
    {
        List<AttachedImageViewModel> orderedItems = Items?.ToList()
            ?? [];
        int sourceIndex = orderedItems.IndexOf(draggedItem);
        if (sourceIndex < 0)
        {
            return orderedItems;
        }

        orderedItems.RemoveAt(sourceIndex);
        int clampedTargetIndex = Math.Clamp(targetIndex, 0, orderedItems.Count);
        orderedItems.Insert(clampedTargetIndex, draggedItem);

        return orderedItems;
    }

    private int GetItemIndex(AttachedImageViewModel item)
    {
        List<AttachedImageViewModel> items = Items?.ToList()
            ?? [];

        return items.IndexOf(item);
    }

    private void CompleteDrag(AttachmentDragCompletion completion)
    {
        AttachmentDragState? dragState = _dragState;
        if (dragState is null)
        {
            return;
        }

        _dragState = null;
        dragState.Entry.Control.ZIndex = 0;

        if (completion == AttachmentDragCompletion.Commit
            && dragState.TargetIndex != dragState.SourceIndex
            && ReorderAttachmentCommand is not null)
        {
            AttachedImageReorderRequest request = new(
                dragState.Entry.Item,
                dragState.TargetIndex);
            if (ReorderAttachmentCommand.CanExecute(request))
            {
                ReorderAttachmentCommand.Execute(request);
                return;
            }
        }

        SynchronizeItems(AttachmentMutationMode.Animated);
    }

    private void CancelDrag()
    {
        _dragCandidate = null;

        if (_dragState is null)
        {
            return;
        }

        CompleteDrag(AttachmentDragCompletion.Cancel);
    }

    private void CompleteRemoveAnimation(AttachmentVisualEntry entry)
    {
        AttachmentOverlayCanvas.Children.Remove(entry.Control);
        _removingEntries.Remove(entry);
        entry.DisposeVisual();
    }

    private void RevealReadyImage(AttachmentVisualEntry entry)
    {
        if (!_entries.TryGetValue(entry.Item.Id, out AttachmentVisualEntry? activeEntry)
            || !ReferenceEquals(activeEntry, entry))
        {
            return;
        }

        entry.Image.IsVisible = true;

        if (_animationScheduler is null)
        {
            MotionFrameApplier.Apply(
                entry.Image,
                MotionFrame.Identity);
            return;
        }

        _animationScheduler.Cancel([entry.Image]);
        _ = _animationScheduler.AnimateAsync(
            entry.Image,
            new List<MotionFrame>
            {
                new(0d, 0d, 1d, 0d, 0d),
                MotionFrame.Identity
            },
            ImageRevealDurationMilliseconds,
            0,
            MotionEasing.EaseOut);
    }

    private void UpdateRemoveCommands()
    {
        foreach (AttachmentVisualEntry entry in _entries.Values)
        {
            entry.RemoveButton.Command = RemoveAttachmentCommand;
        }
    }

    private void UpdatePanelSize(int count)
    {
        double previewSize = GetPreviewSize();
        AttachmentPanel.Width = count > 0 ? count * GetSlotWidth() : 0d;
        AttachmentPanel.Height = previewSize;
        AttachmentOverlayCanvas.Height = previewSize;
    }

    private bool ShouldScrollToEndAfterCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null || e.NewItems.Count == 0)
        {
            return false;
        }

        if (e.NewStartingIndex < 0)
        {
            return true;
        }

        int itemCount = Items?.Count() ?? 0;

        return e.NewStartingIndex + e.NewItems.Count >= itemCount;
    }

    private void ScrollToEnd()
    {
        double targetOffsetX = GetHorizontalScrollEndOffset();

        if (targetOffsetX <= AttachmentScrollViewer.Offset.X + MovementTolerance)
        {
            return;
        }

        SmoothScrollBehavior.ScrollToOffset(
            AttachmentScrollViewer,
            new Vector(targetOffsetX, AttachmentScrollViewer.Offset.Y));
    }

    private double GetHorizontalScrollEndOffset()
    {
        double viewportWidth = AttachmentScrollViewer.Viewport.Width;

        if (viewportWidth <= 0d)
        {
            viewportWidth = AttachmentScrollViewer.Bounds.Width;
        }

        return Math.Max(0d, AttachmentPanel.Width - viewportWidth);
    }

    private double GetSlotWidth()
    {
        return GetPreviewSize() + GetPreviewGap();
    }

    private double GetPreviewSize()
    {
        if (this.TryFindResource("AttachmentItemSize", out object? value)
            && value is double size)
        {
            return size;
        }

        return DefaultPreviewSize;
    }

    private double GetPreviewGap()
    {
        if (this.TryFindResource("AttachmentPreviewMargin", out object? value)
            && value is Thickness margin)
        {
            return margin.Right;
        }

        return DefaultPreviewGap;
    }

    private void EnsureAnimationScheduler()
    {
        if (_animationScheduler is not null)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        _animationScheduler = new GalleryAnimationScheduler(new AvaloniaUiFrameScheduler(topLevel));
    }

    private void CancelAllAnimations()
    {
        if (_animationScheduler is null)
        {
            return;
        }

        List<Control> controls = _entries.Values
            .Concat(_removingEntries)
            .SelectMany(GetEntryAnimationControls)
            .ToList();
        _animationScheduler.Cancel(controls);
    }

    private enum AttachmentMutationMode
    {
        Instant,
        Animated
    }

    private enum AttachmentDragCompletion
    {
        Commit,
        Cancel
    }

    private sealed record AttachmentDragState(
        AttachmentVisualEntry Entry,
        double PointerOffsetX,
        int SourceIndex,
        int TargetIndex);

    private sealed record AttachmentDragCandidate(
        AttachmentVisualEntry Entry,
        Point Origin,
        double PointerOffsetX,
        int SourceIndex,
        int TargetIndex);

    private sealed record AttachmentMotion(
        double X,
        double Y,
        double Rotate);
}
