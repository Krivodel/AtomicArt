using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class FailedGenerationItemStatusDescriptor : IRegisteredGenerationItemStatusDescriptor
{
    public GenerationItemStatus Status => GenerationItemStatus.Failed;
    public string DisplayText => "Ошибка";
    public GenerationItemVisualState VisualState => GenerationItemVisualState.Failed;
    public GenerationResultContentPolicy ResultContentPolicy => GenerationResultContentPolicy.Ignore;
}
