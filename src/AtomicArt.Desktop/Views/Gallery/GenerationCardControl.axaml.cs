using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Controls.Gallery;
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

    public GenerationCardControl()
    {
        InitializeComponent();
        GenerationPreview.OverflowOwner = this;
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
}
