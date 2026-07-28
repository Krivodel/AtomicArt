namespace AtomicArt.Desktop.Services;

public sealed class DataTransferOptions
{
    public const string SectionName = "DataTransfer";

    public int MaximumTransferredFileNameCharacters { get; init; }
    public int MaximumVirtualFileCount { get; init; }
    public int MaximumVirtualFileDescriptorBytes { get; init; }
    public int VirtualFileStreamBufferSize { get; init; }

    public static bool IsValid(DataTransferOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.MaximumTransferredFileNameCharacters > 0
            && options.MaximumVirtualFileCount > 0
            && options.MaximumVirtualFileDescriptorBytes > 0
            && options.VirtualFileStreamBufferSize > 0;
    }
}
