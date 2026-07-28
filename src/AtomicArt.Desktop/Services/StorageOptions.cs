namespace AtomicArt.Desktop.Services;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public int DataRootFileTransferBufferSize { get; init; }
    public int MaximumProtectedSecretFileBytes { get; init; }
    public int TrustedFileStreamBufferSize { get; init; }

    public static bool IsValid(StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.DataRootFileTransferBufferSize > 0
            && options.MaximumProtectedSecretFileBytes > 0
            && options.TrustedFileStreamBufferSize > 0;
    }
}
