using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationItemStatusDescriptor
{
    GenerationItemStatus Status { get; }
    string DisplayText { get; }
    GenerationItemVisualState VisualState { get; }
    GenerationResultContentPolicy ResultContentPolicy { get; }
}
