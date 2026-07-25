namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationAdmissionGate
{
    Task<GenerationAdmissionLease> EnterAsync(CancellationToken ct);
    Task<GenerationAdmissionPause> PauseAsync(CancellationToken ct);
}
