namespace AtomicArt.Desktop.Services.Gallery;

internal abstract class GalleryOperation
{
    public IReadOnlyList<object> Items { get; }
    public Guid? ItemId { get; }
    public TaskCompletionSource Completion { get; }

    protected GalleryOperation(
        IReadOnlyList<object> items,
        Guid? itemId)
    {
        ArgumentNullException.ThrowIfNull(items);

        Items = items.ToArray();
        ItemId = itemId;
        Completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
