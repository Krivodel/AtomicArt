namespace AtomicArt.Desktop.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(CancellationToken ct);
}
