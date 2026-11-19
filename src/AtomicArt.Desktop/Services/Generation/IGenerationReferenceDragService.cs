using Avalonia.Controls;
using Avalonia.Input;

namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationReferenceDragService
{
    Task DragAsync(
        Control source,
        PointerPressedEventArgs pressed,
        GenerationReference reference,
        CancellationToken ct);
}
