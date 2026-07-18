using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class FailedGenerationItemStatusDescriptor : IRegisteredGenerationItemStatusDescriptor
{
    public GenerationItemStatus Status => GenerationItemStatus.Failed;
    public string DisplayText => UiStrings.Error;
    public GenerationItemVisualState VisualState => GenerationItemVisualState.Failed;
    public GenerationResultContentPolicy ResultContentPolicy => GenerationResultContentPolicy.Ignore;
}
