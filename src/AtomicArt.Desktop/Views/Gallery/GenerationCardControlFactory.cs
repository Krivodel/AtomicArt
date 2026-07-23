using Avalonia.Controls;

using AtomicArt.Desktop.Controls.Gallery;

namespace AtomicArt.Desktop.Views.Gallery;

internal sealed class GenerationCardControlFactory : IGalleryCardControlFactory
{
    public Control Create(
        object item,
        GalleryCardCommands commands,
        IGenerationPreviewExpansionHost previewExpansionHost)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(previewExpansionHost);

        GenerationCardControl control = new()
        {
            PreviewExpansionHost = previewExpansionHost
        };
        ApplyCommands(control, commands);

        return control;
    }

    public void ApplyCommands(Control control, GalleryCardCommands commands)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(commands);

        if (control is not GenerationCardControl generationCard)
        {
            throw new ArgumentException(
                $"Expected {nameof(GenerationCardControl)} control.",
                nameof(control));
        }

        SetCommand(generationCard, nameof(GenerationCardControl.OpenViewerCommand), commands.OpenViewer);
        SetCommand(generationCard, nameof(GenerationCardControl.RevealInFolderCommand), commands.RevealInFolder);
        SetCommand(generationCard, nameof(GenerationCardControl.OpenMetadataCommand), commands.OpenMetadata);
        SetCommand(generationCard, nameof(GenerationCardControl.DeleteOrCancelCommand), commands.DeleteOrCancel);
    }

    private static void SetCommand(GenerationCardControl control, string propertyName, object? command)
    {
        typeof(GenerationCardControl)
            .GetProperty(propertyName)
            ?.SetValue(control, command);
    }
}
