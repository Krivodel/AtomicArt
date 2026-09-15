using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Pica.Viewer.Services;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Views.Gallery;

public partial class GenerationCardControl :
    UserControl,
    IGalleryCardSurfaceProvider,
    IGalleryRemovalAnimationParticipant
{
    public IRelayCommand? RevealInFolderCommand
    {
        get => GetValue(GenerationCardControl.RevealInFolderCommandProperty);
        set => SetValue(GenerationCardControl.RevealInFolderCommandProperty, value);
    }
    public IRelayCommand? RevealInNewFolderWindowCommand
    {
        get => GetValue(GenerationCardControl.RevealInNewFolderWindowCommandProperty);
        set => SetValue(GenerationCardControl.RevealInNewFolderWindowCommandProperty, value);
    }
    public IRelayCommand? OpenViewerCommand
    {
        get => GetValue(GenerationCardControl.OpenViewerCommandProperty);
        set => SetValue(GenerationCardControl.OpenViewerCommandProperty, value);
    }
    public IRelayCommand? ShowFailureDetailsCommand
    {
        get => GetValue(GenerationCardControl.ShowFailureDetailsCommandProperty);
        set => SetValue(GenerationCardControl.ShowFailureDetailsCommandProperty, value);
    }
    public IRelayCommand? OpenMetadataCommand
    {
        get => GetValue(GenerationCardControl.OpenMetadataCommandProperty);
        set => SetValue(GenerationCardControl.OpenMetadataCommandProperty, value);
    }
    public IRelayCommand? DeleteOrCancelCommand
    {
        get => GetValue(GenerationCardControl.DeleteOrCancelCommandProperty);
        set => SetValue(GenerationCardControl.DeleteOrCancelCommandProperty, value);
    }
    public IRelayCommand? ToggleFavoriteCommand
    {
        get => GetValue(GenerationCardControl.ToggleFavoriteCommandProperty);
        set => SetValue(GenerationCardControl.ToggleFavoriteCommandProperty, value);
    }
    public IRelayCommand? OpenDlss5Command
    {
        get => GetValue(GenerationCardControl.OpenDlss5CommandProperty);
        set => SetValue(GenerationCardControl.OpenDlss5CommandProperty, value);
    }
    public IRelayCommand? ToggleSelectionCommand
    {
        get => GetValue(GenerationCardControl.ToggleSelectionCommandProperty);
        set => SetValue(GenerationCardControl.ToggleSelectionCommandProperty, value);
    }
    public IRelayCommand? SelectRangeCommand
    {
        get => GetValue(GenerationCardControl.SelectRangeCommandProperty);
        set => SetValue(GenerationCardControl.SelectRangeCommandProperty, value);
    }
    public bool IsSelectionMode
    {
        get => GetValue(GenerationCardControl.IsSelectionModeProperty);
        set => SetValue(GenerationCardControl.IsSelectionModeProperty, value);
    }
    public bool IsSelectionDimmed
    {
        get => GetValue(GenerationCardControl.IsSelectionDimmedProperty);
        set => SetValue(GenerationCardControl.IsSelectionDimmedProperty, value);
    }

    public static readonly StyledProperty<IRelayCommand?> RevealInFolderCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(RevealInFolderCommand));
    public static readonly StyledProperty<IRelayCommand?> RevealInNewFolderWindowCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(RevealInNewFolderWindowCommand));
    public static readonly StyledProperty<IRelayCommand?> OpenViewerCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(OpenViewerCommand));
    public static readonly StyledProperty<IRelayCommand?> ShowFailureDetailsCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(ShowFailureDetailsCommand));
    public static readonly StyledProperty<IRelayCommand?> OpenMetadataCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(OpenMetadataCommand));
    public static readonly StyledProperty<IRelayCommand?> DeleteOrCancelCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(DeleteOrCancelCommand));
    public static readonly StyledProperty<IRelayCommand?> ToggleFavoriteCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(ToggleFavoriteCommand));
    public static readonly StyledProperty<IRelayCommand?> OpenDlss5CommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(OpenDlss5Command));
    public static readonly StyledProperty<IRelayCommand?> ToggleSelectionCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(ToggleSelectionCommand));
    public static readonly StyledProperty<IRelayCommand?> SelectRangeCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(SelectRangeCommand));
    public static readonly StyledProperty<bool> IsSelectionModeProperty =
        AvaloniaProperty.Register<GenerationCardControl, bool>(
            nameof(IsSelectionMode));
    public static readonly StyledProperty<bool> IsSelectionDimmedProperty =
        AvaloniaProperty.Register<GenerationCardControl, bool>(
            nameof(IsSelectionDimmed));

    internal IGenerationPreviewExpansionHost? PreviewExpansionHost
    {
        get => GenerationPreview.ExpansionHost;
        set => GenerationPreview.ExpansionHost = value;
    }

    private PromptDragCandidate? _promptDragCandidate;
    private bool _isPromptDragActive;

    public GenerationCardControl()
    {
        InitializeComponent();
        GenerationPreview.OverflowOwner = this;
        AttachPromptDragHandlers();
    }

    Control IGalleryCardSurfaceProvider.CardSurface => GenerationCardRoot;

    void IGalleryRemovalAnimationParticipant.PrepareForRemovalTransfer()
    {
        GenerationPreview.PrepareForRemovalTransfer();
    }

    void IGalleryRemovalAnimationParticipant.BeginRemovalAnimation(
        int durationMilliseconds)
    {
        GenerationPreview.BeginRemovalAnimation(durationMilliseconds);
    }

    internal static string? GetImageDragPathOrDefault(GenerationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return GenerationPreviewControl.GetImageDragPathOrDefault(item);
    }

    internal static string? GetImageDragPreviewPathOrDefault(GenerationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return GenerationPreviewControl.GetImageDragPreviewPathOrDefault(item);
    }

    internal static DataTransfer CreateImageFileDataTransfer(IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return GenerationPreviewControl.CreateImageFileDataTransfer(file);
    }

    internal static IRelayCommand? ResolveFileRevealCommand(
        KeyModifiers modifiers,
        IRelayCommand? defaultCommand,
        IRelayCommand? openNewWindowCommand)
    {
        return AlternateActionModifierPolicy.IsActive(modifiers)
            ? openNewWindowCommand
            : defaultCommand;
    }

    internal static IRelayCommand? ResolveSelectionCommand(
        KeyModifiers modifiers,
        IRelayCommand? toggleCommand,
        IRelayCommand? rangeCommand)
    {
        return modifiers.HasFlag(KeyModifiers.Shift)
            ? rangeCommand
            : toggleCommand;
    }

    internal bool IsSelectionGestureBlockedHit(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);

        return (GenerationCardControl.IsVisualOrDescendantOf(visual, RevealInFolderButton))
            || (GenerationCardControl.IsVisualOrDescendantOf(visual, DeleteButton));
    }

    internal void SetPreviewBitmapServices(
        IGalleryPreviewBitmapProvider previewBitmapProvider,
        GalleryPreviewSourceScheduler previewSourceScheduler)
    {
        GenerationPreview.SetPreviewBitmapServices(
            previewBitmapProvider,
            previewSourceScheduler);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GenerationCardControl.IsSelectionDimmedProperty)
        {
            PseudoClasses.Set(
                ":selection-dimmed",
                change.GetNewValue<bool>());
        }
    }

    private static bool IsVisualOrDescendantOf(Visual visual, Visual ancestor)
    {
        return (ReferenceEquals(visual, ancestor))
            || (visual.GetVisualAncestors().Contains(ancestor));
    }

    private void ExecuteOpenMetadata(GenerationItemViewModel item)
    {
        IRelayCommand? command = OpenMetadataCommand;

        if (command?.CanExecute(item) == true)
        {
            command.Execute(item);
        }
    }

    private void AttachPromptDragHandlers()
    {
        PromptDragSource.AddHandler(
            PointerPressedEvent,
            OnPromptDragSourcePointerPressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        PromptDragSource.AddHandler(
            PointerMovedEvent,
            OnPromptDragSourcePointerMoved,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        PromptDragSource.AddHandler(
            PointerReleasedEvent,
            OnPromptDragSourcePointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        PromptDragSource.PointerCaptureLost +=
            OnPromptDragSourcePointerCaptureLost;
    }

    private void OnRevealInFolderClick(object? sender, RoutedEventArgs eventArgs)
    {
        _ = sender;

        if (DataContext is not GenerationItemViewModel item)
        {
            return;
        }

        KeyModifiers modifiers = PreviewExpansionHost?.CurrentKeyModifiers
            ?? KeyModifiers.None;
        IRelayCommand? command = GenerationCardControl.ResolveFileRevealCommand(
            modifiers,
            RevealInFolderCommand,
            RevealInNewFolderWindowCommand);

        if (command?.CanExecute(item) == true)
        {
            command.Execute(item);
            eventArgs.Handled = true;
        }
    }

    private void OnSelectionClick(object? sender, RoutedEventArgs eventArgs)
    {
        _ = sender;

        if (DataContext is not GenerationItemViewModel item)
        {
            return;
        }

        KeyModifiers modifiers = PreviewExpansionHost?.CurrentKeyModifiers
            ?? KeyModifiers.None;
        IRelayCommand? command = GenerationCardControl.ResolveSelectionCommand(
            modifiers,
            ToggleSelectionCommand,
            SelectRangeCommand);

        if (command?.CanExecute(item) == true)
        {
            command.Execute(item);
            eventArgs.Handled = true;
        }
    }

    private void OnPromptDragSourcePointerPressed(
        object? sender,
        PointerPressedEventArgs eventArgs)
    {
        _ = sender;

        if (DataContext is not GenerationItemViewModel item)
        {
            return;
        }

        PointerPoint pointerPoint = eventArgs.GetCurrentPoint(this);
        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        eventArgs.Pointer.Capture(PromptDragSource);
        _promptDragCandidate = new PromptDragCandidate(
            eventArgs,
            pointerPoint.Position,
            item.Prompt);
    }

    private async void OnPromptDragSourcePointerMoved(
        object? sender,
        PointerEventArgs eventArgs)
    {
        _ = sender;

        PromptDragCandidate? dragCandidate = _promptDragCandidate;
        if (dragCandidate is null)
        {
            return;
        }

        PointerPoint pointerPoint = eventArgs.GetCurrentPoint(this);
        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            _promptDragCandidate = null;
            return;
        }

        if ((!PointerDragThreshold.IsReached(
                dragCandidate.Origin,
                pointerPoint.Position))
            || (string.IsNullOrWhiteSpace(dragCandidate.Prompt)))
        {
            return;
        }

        _promptDragCandidate = null;
        _isPromptDragActive = true;
        eventArgs.Handled = true;

        try
        {
            DataTransfer dataTransfer = AtomicArtPromptDragData.Create(
                dragCandidate.Prompt);
            await DragDrop.DoDragDropAsync(
                dragCandidate.PointerPressedEventArgs,
                dataTransfer,
                DragDropEffects.Copy);
        }
        finally
        {
            _isPromptDragActive = false;
            eventArgs.Pointer.Capture(null);
        }
    }

    private void OnPromptDragSourcePointerReleased(
        object? sender,
        PointerReleasedEventArgs eventArgs)
    {
        _ = sender;

        _promptDragCandidate = null;
        eventArgs.Pointer.Capture(null);
    }

    private void OnPromptDragSourcePointerCaptureLost(
        object? sender,
        PointerCaptureLostEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        _promptDragCandidate = null;
    }

    private void OnPromptDragSourceClick(object? sender, RoutedEventArgs eventArgs)
    {
        _ = sender;

        if ((_isPromptDragActive)
            || (DataContext is not GenerationItemViewModel item))
        {
            return;
        }

        ExecuteOpenMetadata(item);
        eventArgs.Handled = true;
    }

    private sealed record PromptDragCandidate(
        PointerPressedEventArgs PointerPressedEventArgs,
        Point Origin,
        string Prompt);
}
