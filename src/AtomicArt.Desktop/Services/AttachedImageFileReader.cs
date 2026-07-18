using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Platform.Storage;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public sealed class AttachedImageFileReader
{
    private const string UnknownImageContentType = "application/octet-stream";

    private readonly IAttachedImageSignatureValidator _signatureValidator;
    private readonly ILogger<AttachedImageFileReader> _logger;

    public AttachedImageFileReader(IAttachedImageSignatureValidator signatureValidator)
        : this(signatureValidator, NullLogger<AttachedImageFileReader>.Instance)
    {
    }

    public AttachedImageFileReader(
        IAttachedImageSignatureValidator signatureValidator,
        ILogger<AttachedImageFileReader> logger)
    {
        ArgumentNullException.ThrowIfNull(signatureValidator);
        ArgumentNullException.ThrowIfNull(logger);

        _signatureValidator = signatureValidator;
        _logger = logger;
    }

    public ImageAttachmentInput CreateInput(
        IStorageFile file,
        int maxInputBytes)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);

        return new ImageAttachmentInput(
            file.Name,
            ct => ReadFileAsync(file, maxInputBytes, ct));
    }

    public IReadOnlyList<ImageAttachmentInput> CreateInputs(
        IReadOnlyList<IStorageFile> files,
        int maxInputBytes)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
        _logger.LogInformation(
            "Created deferred attachment inputs for {FileCount} selected files.",
            files.Count);

        return files
            .Select(file => CreateInput(file, maxInputBytes))
            .ToList();
    }

    private async Task<AttachedImageDto?> ReadFileAsync(
        IStorageFile file,
        int maxInputBytes,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (await IsFileTooLargeAsync(file, maxInputBytes).ConfigureAwait(false))
        {
            _logger.LogWarning(
                "Selected attachment exceeded the configured input limit of {MaxInputBytes} bytes.",
                maxInputBytes);
            throw new InvalidDataException(
                "Attached image exceeds the safe input size limit.");
        }

        await using Stream input = await file.OpenReadAsync()
            .ConfigureAwait(false);
        byte[] content = await ReadLimitedContentAsync(input, maxInputBytes, ct)
            .ConfigureAwait(false);
        bool signatureRecognized = _signatureValidator.TryGetContentType(
            file.Name,
            content,
            out string detectedContentType);
        string contentType = signatureRecognized
            ? detectedContentType
            : UnknownImageContentType;
        _logger.LogInformation(
            "Selected attachment read with {SizeBytes} bytes, recognized signature {SignatureRecognized}, and content type {ContentType}.",
            content.LongLength,
            signatureRecognized,
            contentType);

        return new AttachedImageDto(
            file.Name,
            contentType,
            content);
    }

    private static async Task<bool> IsFileTooLargeAsync(IStorageFile file, int maxInputBytes)
    {
        StorageItemProperties properties = await file.GetBasicPropertiesAsync()
            .ConfigureAwait(false);
        object? sizeValue = properties.Size;

        if (sizeValue is ulong unsignedSize)
        {
            return unsignedSize > (ulong)maxInputBytes;
        }

        if (sizeValue is long signedSize)
        {
            return signedSize > maxInputBytes;
        }

        return false;
    }

    private static async Task<byte[]> ReadLimitedContentAsync(
        Stream input,
        int maxInputBytes,
        CancellationToken ct)
    {
        await using LimitedMemoryStream output = new(maxInputBytes);

        try
        {
            await input.CopyToAsync(output, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidDataException(
                "Attached image exceeds the safe input size limit.",
                ex);
        }

        return output.ToArray();
    }
}
