namespace AtomicArt.Desktop.Services.Generation;

public sealed record GenerationChatResult(string Text, GenerationReference? Image, Guid? StorageLeaseId = null);
