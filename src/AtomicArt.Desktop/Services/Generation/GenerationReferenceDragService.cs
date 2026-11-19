using Avalonia.Controls;
using Avalonia.Input;

using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Controls;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class GenerationReferenceDragService : IGenerationReferenceDragService
{
    private readonly IGenerationReferenceStore _references;
    private readonly ILogger<GenerationReferenceDragService> _logger;

    public GenerationReferenceDragService(
        IGenerationReferenceStore references,
        ILogger<GenerationReferenceDragService> logger)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task DragAsync(
        Control source,
        PointerPressedEventArgs pressed,
        GenerationReference reference,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(reference);

        try
        {
            string? path = await _references.GetExistingFilePathAsync(reference, ct);
            if (path is not null && ReferenceEquals(pressed.Pointer.Captured, source))
            {
                await ImageFileDragSource.StartAsync(
                    source, pressed, path, path, AtomicArtImageDragSourceKind.Gallery);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start drag-and-drop for a saved reference.");
        }
    }
}
