using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class GeneratingGenerationItemStatusDescriptor : IRegisteredGenerationItemStatusDescriptor
{
    public GenerationItemStatus Status => GenerationItemStatus.Generating;
    public string DisplayText => "Генерируется...";
    public GenerationItemVisualState VisualState => GenerationItemVisualState.Generating;
    public GenerationResultContentPolicy ResultContentPolicy => GenerationResultContentPolicy.Ignore;
}
