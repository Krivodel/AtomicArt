using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Platform.Storage;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public sealed class AttachedImageFileReader
{
    private const string UnknownImageContentType = "application/octet-stream";
    private const string AttachedImageTooLargeMessage =
        "Attached image exceeds the safe input size limit.";
    private const int FileStreamBufferSize = 81920;

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

    internal ImageAttachmentInput CreateInput(
        string filePath,
        int maxInputBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);

        string fullPath = Path.GetFullPath(filePath);

        return new ImageAttachmentInput(
            Path.GetFileName(fullPath),
            ct => ReadFileAsync(fullPath, maxInputBytes, ct));
    }

    internal ImageAttachmentInput CreateBufferedInput(
        string fileName,
        byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        return new ImageAttachmentInput(
            fileName,
            ct =>
            {
                ct.ThrowIfCancellationRequested();

                return Task.FromResult<AttachedImageDto?>(
                    CreateImage(fileName, content));
            });
    }

    internal ImageAttachmentInput CaptureInput(IStorageFile file, int maxInputBytes)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);

        string? filePath = file.TryGetLocalPath();

        if (filePath is null)
        {
            return CreateInput(file, maxInputBytes);
        }

        string fileName = file.Name;
        FileStream? input = null;

        try
        {
            input = OpenCapturedReadStream(filePath);
            return CreateCapturedInput(fileName, input, maxInputBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            input?.Dispose();
            return CreateCaptureError(fileName, ex);
        }
    }

    internal ImageAttachmentInput? TryCaptureInput(IStorageFile file, int maxInputBytes)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);

        string? filePath = file.TryGetLocalPath();

        return filePath is null
            ? CreateInput(file, maxInputBytes)
            : TryCaptureInput(filePath, file.Name, maxInputBytes);
    }

    internal ImageAttachmentInput? TryCaptureInput(string filePath, int maxInputBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);

        string fullPath = Path.GetFullPath(filePath);

        return TryCaptureInput(fullPath, Path.GetFileName(fullPath), maxInputBytes);
    }

    private static FileStream OpenCapturedReadStream(string filePath)
    {
        return OpenReadStream(filePath, FileShare.Read | FileShare.Delete);
    }

    private static FileStream OpenReadStream(string filePath, FileShare share)
    {
        return new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            share,
            FileStreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
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

    private ImageAttachmentInput? TryCaptureInput(
        string filePath,
        string fileName,
        int maxInputBytes)
    {
        FileStream? input = null;

        try
        {
            input = OpenCapturedReadStream(filePath);

            if (input.Length == 0)
            {
                input.Dispose();
                return null;
            }

            return CreateCapturedInput(fileName, input, maxInputBytes);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            input?.Dispose();
            _logger.LogDebug(ex, "The clipboard attachment file is no longer available.");
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            input?.Dispose();
            return CreateCaptureError(fileName, ex);
        }
    }

    private ImageAttachmentInput CreateCapturedInput(
        string fileName,
        FileStream input,
        int maxInputBytes)
    {
        if (input.Length > maxInputBytes)
        {
            input.Dispose();

            return ImageAttachmentInput.FromError(
                fileName, new InvalidDataException(AttachedImageTooLargeMessage));
        }

        return new ImageAttachmentInput(
            fileName,
            async ct => await ReadStreamAsync(fileName, input, maxInputBytes, ct)
                .ConfigureAwait(false),
            input);
    }

    private ImageAttachmentInput CreateCaptureError(string fileName, Exception error)
    {
        _logger.LogWarning(error, "The attachment could not acquire a read handle.");

        return ImageAttachmentInput.FromError(fileName, error);
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
                AttachedImageTooLargeMessage);
        }

        await using Stream input = await file.OpenReadAsync()
            .ConfigureAwait(false);
        return await ReadStreamAsync(file.Name, input, maxInputBytes, ct)
            .ConfigureAwait(false);
    }

    private async Task<AttachedImageDto?> ReadFileAsync(
        string filePath,
        int maxInputBytes,
        CancellationToken ct)
    {
        FileInfo file = new(filePath);

        if (file.Length > maxInputBytes)
        {
            _logger.LogWarning(
                "Selected attachment exceeded the configured input limit of {MaxInputBytes} bytes.",
                maxInputBytes);
            throw new InvalidDataException(AttachedImageTooLargeMessage);
        }

        await using FileStream input = OpenReadStream(file.FullName, FileShare.Read);

        return await ReadStreamAsync(file.Name, input, maxInputBytes, ct)
            .ConfigureAwait(false);
    }

    private async Task<AttachedImageDto> ReadStreamAsync(
        string fileName,
        Stream input,
        int maxInputBytes,
        CancellationToken ct)
    {
        byte[] content = await LimitedContentReader
            .ReadAsync(
                input,
                maxInputBytes,
                AttachedImageTooLargeMessage,
                ct)
            .ConfigureAwait(false);
        AttachedImageDto image = CreateImage(fileName, content);
        _logger.LogInformation(
            "Selected attachment read with {SizeBytes} bytes, recognized signature {SignatureRecognized}, and content type {ContentType}.",
            content.LongLength,
            image.ContentType != UnknownImageContentType,
            image.ContentType);

        return image;
    }

    private AttachedImageDto CreateImage(string fileName, byte[] content)
    {
        bool signatureRecognized = _signatureValidator.TryGetContentType(
            fileName,
            content,
            out string detectedContentType);
        string contentType = signatureRecognized
            ? detectedContentType
            : UnknownImageContentType;

        return new AttachedImageDto(fileName, contentType, content);
    }

}
