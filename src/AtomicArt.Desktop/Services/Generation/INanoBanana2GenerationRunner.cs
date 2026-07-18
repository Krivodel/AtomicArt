namespace AtomicArt.Desktop.Services.Generation;

public interface INanoBanana2GenerationRunner
{
    Task RunAsync(
        NanoBanana2GenerationParameters parameters,
        string providerCredential,
        CancellationToken ct);
}
