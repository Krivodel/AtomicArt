namespace AtomicArt.Desktop.Services;

public interface ISoundFilePickerService
{
    Task<string?> PickSoundAsync(CancellationToken ct);
}
