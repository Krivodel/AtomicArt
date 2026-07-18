using System.Security.Cryptography;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation;

internal sealed class FileSystemPlaceholderImageProvider : PlaceholderImageProvider
{
    private static readonly int MaxSignatureBytes = GenerationImageFileFormats.All
        .SelectMany(format => format.SignatureAlternatives)
        .SelectMany(alternative => alternative)
        .Select(part => part.Offset + part.Bytes.Count)
        .DefaultIfEmpty(0)
        .Max();

    private readonly IOptions<TestGenerationOptions> _options;
    private readonly ILogger<FileSystemPlaceholderImageProvider> _logger;

    public FileSystemPlaceholderImageProvider(IOptions<TestGenerationOptions> options)
        : this(options, NullLogger<FileSystemPlaceholderImageProvider>.Instance)
    {
    }

    public FileSystemPlaceholderImageProvider(
        IOptions<TestGenerationOptions> options,
        ILogger<FileSystemPlaceholderImageProvider> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<PlaceholderImage> GetNextCoreAsync(
        string modelId,
        int itemIndex,
        CancellationToken ct)
    {
        TestGenerationOptions options = _options.Value;
        List<string> candidatePaths = await CreateCandidatePathsAsync(options, ct)
            .ConfigureAwait(false);
        int candidateCount = candidatePaths.Count;

        _logger.LogInformation(
            "Test image search completed with {CandidateCount} matching files.",
            candidateCount);

        while (candidatePaths.Count > 0)
        {
            int index = RandomNumberGenerator.GetInt32(candidatePaths.Count);
            string path = candidatePaths[index];
            candidatePaths.RemoveAt(index);

            PlaceholderImage? image = await TryReadImageAsync(path, options.MaxImageBytes, ct)
                .ConfigureAwait(false);

            if (image is not null)
            {
                _logger.LogInformation(
                    "Test image was read successfully. Content type: {ContentType}; size: {ContentLength} bytes.",
                    image.ContentType,
                    image.Content.Length);

                return image;
            }
        }

        _logger.LogWarning(
            "No supported test image was found among {CandidateCount} candidates.",
            candidateCount);

        throw new ImageGenerationProviderException(
            ImageGenerationProviderFailureKind.Unavailable,
            "The test generation directory does not contain a supported image.");
    }

    private async Task<List<string>> CreateCandidatePathsAsync(
        TestGenerationOptions options,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.ImagesDirectory))
        {
            _logger.LogWarning(
                "Test image directory is not configured.");

            return [];
        }

        if (!Directory.Exists(options.ImagesDirectory))
        {
            _logger.LogWarning(
                "Configured test image directory does not exist.");

            return [];
        }

        List<string> candidates = [];
        IEnumerable<string> files;

        try
        {
            files = Directory.EnumerateFiles(options.ImagesDirectory);
        }
        catch (IOException exception)
        {
            _logger.LogWarning(
                "Failed to enumerate test images. Failure category: {FailureType}.",
                exception.GetType().Name);

            throw CreateDirectoryReadException();
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                "Access was denied while enumerating test images. Failure category: {FailureType}.",
                exception.GetType().Name);

            throw CreateDirectoryReadException();
        }

        foreach (string path in files)
        {
            ct.ThrowIfCancellationRequested();

            if (await IsSupportedImageAsync(path, options.MaxImageBytes, ct)
                    .ConfigureAwait(false))
            {
                candidates.Add(path);
            }
        }

        return candidates;
    }

    private async Task<bool> IsSupportedImageAsync(
        string path,
        long maxImageBytes,
        CancellationToken ct)
    {
        try
        {
            await using FileStream stream = OpenRead(path);

            if (stream.Length <= 0)
            {
                return false;
            }

            if (stream.Length > maxImageBytes)
            {
                return false;
            }

            return await DetectContentTypeAsync(stream, ct).ConfigureAwait(false) is not null;
        }
        catch (IOException exception)
        {
            _logger.LogDebug(
                "Test image file was skipped during validation. Failure category: {FailureType}.",
                exception.GetType().Name);

            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogDebug(
                "Test image file was skipped because access was denied during validation. Failure category: {FailureType}.",
                exception.GetType().Name);

            return false;
        }
    }

    private async Task<PlaceholderImage?> TryReadImageAsync(
        string path,
        long maxImageBytes,
        CancellationToken ct)
    {
        try
        {
            await using FileStream stream = OpenRead(path);

            if (stream.Length <= 0 || stream.Length > maxImageBytes)
            {
                return null;
            }

            string? contentType = await DetectContentTypeAsync(stream, ct).ConfigureAwait(false);

            if (contentType is null)
            {
                return null;
            }

            stream.Position = 0;
            byte[] content = await BoundedStreamReader
                .ReadToEndAsync(
                    stream,
                    maxImageBytes,
                    CreateImageTooLargeException,
                    ct)
                .ConfigureAwait(false);

            return new PlaceholderImage(contentType, content);
        }
        catch (IOException exception)
        {
            _logger.LogWarning(
                "Failed to read the selected test image. Failure category: {FailureType}.",
                exception.GetType().Name);

            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                "Access was denied while reading the selected test image. Failure category: {FailureType}.",
                exception.GetType().Name);

            return null;
        }
    }

    private static FileStream OpenRead(string path)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: BoundedStreamReader.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private static async Task<string?> DetectContentTypeAsync(
        Stream stream,
        CancellationToken ct)
    {
        byte[] signature = new byte[MaxSignatureBytes];
        int bytesRead = await ReadSignatureAsync(stream, signature, ct).ConfigureAwait(false);

        foreach (GenerationImageFileFormatDescriptor format in GenerationImageFileFormats.All)
        {
            if (GenerationImageSignatureMatcher.Matches(
                    format,
                    signature.AsSpan(0, bytesRead)))
            {
                return format.ContentType;
            }
        }

        return null;
    }

    private static async Task<int> ReadSignatureAsync(
        Stream stream,
        byte[] signature,
        CancellationToken ct)
    {
        int totalBytesRead = 0;

        while (totalBytesRead < signature.Length)
        {
            int bytesRead = await stream
                .ReadAsync(signature.AsMemory(totalBytesRead, signature.Length - totalBytesRead), ct)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }

    private static IOException CreateImageTooLargeException()
    {
        return new IOException("The test image exceeds the allowed size.");
    }

    private static ImageGenerationProviderException CreateDirectoryReadException()
    {
        return new ImageGenerationProviderException(
            ImageGenerationProviderFailureKind.Unavailable,
            "The test generation directory could not be read.");
    }
}
