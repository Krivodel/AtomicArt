using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Views.Gallery;

public partial class GenerationCardControl : UserControl
{
    public IRelayCommand? RevealInFolderCommand
    {
        get => GetValue(RevealInFolderCommandProperty);
        set => SetValue(RevealInFolderCommandProperty, value);
    }
    public IRelayCommand? OpenViewerCommand
    {
        get => GetValue(OpenViewerCommandProperty);
        set => SetValue(OpenViewerCommandProperty, value);
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

    public static readonly StyledProperty<IRelayCommand?> RevealInFolderCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(RevealInFolderCommand));
    public static readonly StyledProperty<IRelayCommand?> OpenViewerCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(OpenViewerCommand));
    public static readonly StyledProperty<IRelayCommand?> OpenMetadataCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(OpenMetadataCommand));
    public static readonly StyledProperty<IRelayCommand?> DeleteOrCancelCommandProperty =
        AvaloniaProperty.Register<GenerationCardControl, IRelayCommand?>(
            nameof(DeleteOrCancelCommand));

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
}
