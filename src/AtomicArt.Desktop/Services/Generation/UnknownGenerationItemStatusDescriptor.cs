using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class UnknownGenerationItemStatusDescriptor : IGenerationItemStatusDescriptor
{
    public UnknownGenerationItemStatusDescriptor(GenerationItemStatus status)
    {
        Status = status;
    }

    public GenerationItemStatus Status { get; }
    public string DisplayText => Status.ToString();
    public GenerationItemVisualState VisualState => GenerationItemVisualState.Unknown;
    public GenerationResultContentPolicy ResultContentPolicy => GenerationResultContentPolicy.Ignore;
}
