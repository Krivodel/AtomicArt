using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class GeneratedGenerationItemStatusDescriptor : IRegisteredGenerationItemStatusDescriptor
{
    public GenerationItemStatus Status => GenerationItemStatus.Generated;
    public string DisplayText => "Готово";
    public GenerationItemVisualState VisualState => GenerationItemVisualState.Generated;
    public GenerationResultContentPolicy ResultContentPolicy => GenerationResultContentPolicy.SaveValidatedContent;
}
