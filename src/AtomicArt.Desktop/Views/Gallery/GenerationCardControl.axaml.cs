using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.ViewModels.Gallery;

using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Views.Gallery;

public partial class GenerationCardControl :
    UserControl,
    IGalleryCardSurfaceProvider,
    IGalleryRemovalAnimationParticipant
{
    public IRelayCommand? RevealInFolderCommand
    {
        get => GetValue(RevealInFolderCommandProperty);
        set => SetValue(RevealInFolderCommandProperty, value);
    }
    public IRelayCommand? RevealInNewFolderWindowCommand
    {
        get => GetValue(RevealInNewFolderWindowCommandProperty);
        set => SetValue(RevealInNewFolderWindowCommandProperty, value);
    }
    public IRelayCommand? OpenViewerCommand
    {
        get => GetValue(OpenViewerCommandProperty);
        set => SetValue(OpenViewerCommandProperty, value);
    }
    public IRelayCommand? ShowFailureDetailsCommand
    {
        get => GetValue(ShowFailureDetailsCommandProperty);
        set => SetValue(ShowFailureDetailsCommandProperty, value);
    }
    public IRelayCommand? OpenMetadataCommand
    {
        get => GetValue(OpenMetadataCommandProperty);
        set => SetValue(OpenMetadataCommandProperty, value);
    }
    public IRelayCommand? DeleteOrCancelCommand
    {
        get => GetValue(DeleteOrCancelCommandProperty);
        set => SetValue(DeleteOrCancelCommandProperty, value);
    }
    public IRelayCommand? ToggleSelectionCommand
    {
        get => GetValue(ToggleSelectionCommandProperty);
        set => SetValue(ToggleSelectionCommandProperty, value);
    }
    public IRelayCommand? SelectRangeCommand
    {
        get => GetValue(SelectRangeCommandProperty);
        set => SetValue(SelectRangeCommandProperty, value);
    }
    public bool IsSelectionMode
    {
        get => GetValue(IsSelectionModeProperty);
        set => SetValue(IsSelectionModeProperty, value);
    }
    public bool IsSelectionDimmed
    {
        get => GetValue(IsSelectionDimmedProperty);
        set => SetValue(IsSelectionDimmedProperty, value);
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

    internal bool IsSelectionToggleHit(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);

        return ReferenceEquals(visual, ToggleSelectionButton)
            || visual.GetVisualAncestors().Contains(ToggleSelectionButton);
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

        if (change.Property == IsSelectionDimmedProperty)
        {
            PseudoClasses.Set(
                ":selection-dimmed",
                change.GetNewValue<bool>());
        }
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

    private void OnRevealInFolderClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;

        if (DataContext is not GenerationItemViewModel item)
        {
            return;
        }

        KeyModifiers modifiers = PreviewExpansionHost?.CurrentKeyModifiers
            ?? KeyModifiers.None;
        IRelayCommand? command = ResolveFileRevealCommand(
            modifiers,
            RevealInFolderCommand,
            RevealInNewFolderWindowCommand);

        if (command?.CanExecute(item) == true)
        {
            command.Execute(item);
            e.Handled = true;
        }
    }

    private void OnSelectionClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;

        if (DataContext is not GenerationItemViewModel item)
        {
            return;
        }

        KeyModifiers modifiers = PreviewExpansionHost?.CurrentKeyModifiers
            ?? KeyModifiers.None;
        IRelayCommand? command = ResolveSelectionCommand(
            modifiers,
            ToggleSelectionCommand,
            SelectRangeCommand);

        if (command?.CanExecute(item) == true)
        {
            command.Execute(item);
            e.Handled = true;
        }
    }

    private void OnPromptDragSourcePointerPressed(
        object? sender,
        PointerPressedEventArgs e)
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

        e.Pointer.Capture(PromptDragSource);
        _promptDragCandidate = new PromptDragCandidate(
            e,
            pointerPoint.Position,
            item.Prompt);
    }

    private async void OnPromptDragSourcePointerMoved(
        object? sender,
        PointerEventArgs e)
    {
        _ = sender;

        PromptDragCandidate? dragCandidate = _promptDragCandidate;
        if (dragCandidate is null)
        {
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(this);
        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            _promptDragCandidate = null;
            return;
        }

        if (!PointerDragThreshold.IsReached(
                dragCandidate.Origin,
                pointerPoint.Position)
            || string.IsNullOrWhiteSpace(dragCandidate.Prompt))
        {
            return;
        }

        _promptDragCandidate = null;
        _isPromptDragActive = true;
        e.Handled = true;

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
            e.Pointer.Capture(null);
        }
    }

    private void OnPromptDragSourcePointerReleased(
        object? sender,
        PointerReleasedEventArgs e)
    {
        _ = sender;

        _promptDragCandidate = null;
        e.Pointer.Capture(null);
    }

    private void OnPromptDragSourcePointerCaptureLost(
        object? sender,
        PointerCaptureLostEventArgs e)
    {
        _ = sender;
        _ = e;

        _promptDragCandidate = null;
    }

    private void OnPromptDragSourceClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;

        if (_isPromptDragActive
            || DataContext is not GenerationItemViewModel item)
        {
            return;
        }

        ExecuteOpenMetadata(item);
        e.Handled = true;
    }

    private sealed record PromptDragCandidate(
        PointerPressedEventArgs PointerPressedEventArgs,
        Point Origin,
        string Prompt);
}
