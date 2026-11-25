using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Generation.State;

public sealed class PanelAttachmentStore :
    IPanelAttachmentStore,
    IPanelAttachmentFilePathResolver
{
    private static readonly string TrustedPathFailureMessage =
        TrustedPathGuard.CreateFailureMessage(
            "Panel attachment path",
            AtomicArtPathNames.StateAttachmentsRelativePath);
    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly IStatePathKeyEncoder _keyEncoder;
    private readonly IGenerationImageFormatRegistry _formatRegistry;
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly ILogger<PanelAttachmentStore> _logger;
    private readonly TrustedFileStreamFactory _trustedFileStreamFactory;
    private readonly IGenerationReferenceStore _references;

    public PanelAttachmentStore(
        IAtomicArtDataPathProvider pathProvider,
        IStatePathKeyEncoder keyEncoder,
        IGenerationImageFormatRegistry formatRegistry,
        IDataRootAccessCoordinator accessCoordinator,
        TrustedFileStreamFactory trustedFileStreamFactory,
        ILogger<PanelAttachmentStore> logger,
        IGenerationReferenceStore references)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _keyEncoder = keyEncoder ?? throw new ArgumentNullException(nameof(keyEncoder));
        _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
        _accessCoordinator = accessCoordinator
            ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _trustedFileStreamFactory = trustedFileStreamFactory
            ?? throw new ArgumentNullException(nameof(trustedFileStreamFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _references = references ?? throw new ArgumentNullException(nameof(references));
    }

    public PanelAttachmentState CreateState(AttachedImageDto image)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(image.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(image.ContentType);
        ArgumentNullException.ThrowIfNull(image.Content);

        if (!_formatRegistry.TryGetByContentType(image.ContentType, out IGenerationImageFormat? format)
            || format is null)
        {
            throw new InvalidOperationException(
                "Panel attachment content type must have a registered image format.");
        }

        string attachmentId = Guid.NewGuid().ToString("N");
        string internalFileName = string.Concat(attachmentId, format.Extension);
        _logger.LogDebug(
            "Created managed panel attachment state {AttachmentId} with content type {ContentType} and {SizeBytes} bytes.",
            attachmentId,
            image.ContentType,
            image.Content.LongLength);

        return new PanelAttachmentState
        {
            Id = attachmentId,
            FileName = CreateSafeDisplayFileName(image.FileName, format.Extension),
            ContentType = image.ContentType,
            SizeBytes = image.Content.LongLength,
            InternalFileName = internalFileName
        };
    }

    public async Task SaveAsync(
        string panelId,
        PanelAttachmentState attachment,
        AttachedImageDto image,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        ArgumentNullException.ThrowIfNull(attachment);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(image.Content);
        if (!PanelAttachmentStateSanitizer.IsValid(attachment))
        {
            throw new InvalidOperationException("Panel attachment state is invalid.");
        }

        if (!string.Equals(attachment.ContentType, image.ContentType, StringComparison.OrdinalIgnoreCase)
            || attachment.SizeBytes != image.Content.LongLength)
        {
            throw new InvalidOperationException("Panel attachment state does not match attachment content.");
        }

        string ownerPath = string.Concat(GetRelativeAttachmentPath(panelId, attachment.InternalFileName),
            GenerationReferenceFilePolicy.OwnerFileExtension);
        AttachedImageDto savedImage = new(attachment.FileName, attachment.ContentType, image.Content);
        await _references.SaveOwnedAsync(ownerPath, savedImage, ct).ConfigureAwait(false);
        using DataRootAccessLease accessLease = await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);
        DeleteFileIfExists(GetAttachmentPath(panelId, attachment.InternalFileName));
        _logger.LogInformation("Managed panel attachment {AttachmentId} saved with {SizeBytes} bytes.",
            attachment.Id, attachment.SizeBytes);
    }

    public async Task<AttachedImageDto?> LoadAsync(
        string panelId,
        PanelAttachmentState attachment,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        ArgumentNullException.ThrowIfNull(attachment);

        try
        {
            GenerationReference? reference;
            AttachedImageDto? image = null;

            using (DataRootAccessLease accessLease = await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false))
            {
                reference = ReadOwnerReference(panelId, attachment);

                if (reference is null)
                {
                    image = await LoadLegacyAttachmentAsync(panelId, attachment, ct).ConfigureAwait(false);
                }
            }

            if (reference is not null)
            {
                return await _references.LoadAsync(reference, ct).ConfigureAwait(false);
            }

            if (image is null)
            {
                return null;
            }

            try
            {
                await SaveAsync(panelId, attachment, image, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _logger.LogWarning(ex, "Could not deduplicate managed panel attachment {AttachmentId}.", attachment.Id);
            }

            return image;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to load managed panel attachment {AttachmentId}.", attachment.Id);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied while loading managed panel attachment {AttachmentId}.", attachment.Id);
            return null;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Managed panel attachment path is not supported for {AttachmentId}.", attachment.Id);
            return null;
        }
    }

    public async Task DeleteAsync(
        string panelId,
        PanelAttachmentState attachment,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        ArgumentNullException.ThrowIfNull(attachment);
        ct.ThrowIfCancellationRequested();
        using DataRootAccessLease accessLease =
            await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);

        string path = GetAttachmentPath(panelId, attachment.InternalFileName);
        DeleteFileIfExists(string.Concat(path, GenerationReferenceFilePolicy.OwnerFileExtension));
        DeleteFileIfExists(path);
        _logger.LogInformation(
            "Managed panel attachment {AttachmentId} deletion completed.",
            attachment.Id);

    }

    public async Task<string?> GetExistingFilePathAsync(
        string panelId,
        PanelAttachmentState attachment,
        CancellationToken ct)
    {
        using DataRootAccessLease accessLease =
            await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);

        return ResolveExistingFilePath(panelId, attachment);
    }

    public string? ResolveExistingFilePath(string panelId, PanelAttachmentState attachment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        ArgumentNullException.ThrowIfNull(attachment);

        try
        {
            if (ReadOwnerReference(panelId, attachment) is { } reference)
            {
                return _references.ResolveExistingFilePath(reference);
            }

            if (!TryOpenLegacyAttachmentFileForRead(
                    panelId,
                    attachment,
                    out FileStream? stream,
                    out string? trustedPath)
                || stream is null
                || trustedPath is null)
            {
                return null;
            }

            stream.Dispose();

            return trustedPath;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to resolve managed panel attachment {AttachmentId}.",
                attachment.Id);

            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                ex,
                "Access denied while resolving managed panel attachment {AttachmentId}.",
                attachment.Id);

            return null;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(
                ex,
                "Managed panel attachment path is not supported for {AttachmentId}.",
                attachment.Id);

            return null;
        }
    }

    private string GetAttachmentPath(string panelId, string internalFileName)
    {
        string root = GetAttachmentsRootDirectory();
        string path = Path.GetFullPath(Path.Combine(root, GetRelativeAttachmentPath(panelId, internalFileName)));
        TrustedPathGuard.EnsureInsideDirectory(root, path, TrustedPathFailureMessage);

        return path;
    }

    private async Task<AttachedImageDto?> LoadLegacyAttachmentAsync(
        string panelId,
        PanelAttachmentState attachment,
        CancellationToken ct)
    {
        if (!TryOpenLegacyAttachmentFileForRead(panelId, attachment, out FileStream? stream, out _)
            || stream is null)
        {
            return null;
        }

        await using (stream.ConfigureAwait(false))
        {
            if (stream.Length > int.MaxValue)
            {
                throw new IOException("Managed panel attachment file is too large to load.");
            }

            byte[] content = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(content, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Managed panel attachment {AttachmentId} loaded with {SizeBytes} bytes.",
                attachment.Id,
                content.LongLength);

            return new AttachedImageDto(attachment.FileName, attachment.ContentType, content);
        }
    }

    private GenerationReference? ReadOwnerReference(string panelId, PanelAttachmentState attachment)
    {
        string ownerPath = string.Concat(GetAttachmentPath(panelId, attachment.InternalFileName),
            GenerationReferenceFilePolicy.OwnerFileExtension);
        string root = GetAttachmentsRootDirectory();

        if (!_trustedFileStreamFactory.TryOpenExistingFileForRead(ownerPath, new string[] { root },
                root, TrustedPathFailureMessage, out FileStream? stream, out _)
            || stream is null)
        {
            return null;
        }

        using (stream)
        {
            string? hash = GenerationReferenceFilePolicy.ReadOwnerHash(stream);

            return hash is null ? null : new GenerationReference(hash, attachment.FileName, attachment.ContentType);
        }
    }

    private bool TryOpenLegacyAttachmentFileForRead(
        string panelId,
        PanelAttachmentState attachment,
        out FileStream? stream,
        out string? trustedPath)
    {
        string path = GetAttachmentPath(panelId, attachment.InternalFileName);
        string attachmentsRootDirectory = GetAttachmentsRootDirectory();
        string[] trustedDirectories = [attachmentsRootDirectory];

        if (!_trustedFileStreamFactory.TryOpenExistingFileForRead(
                path,
                trustedDirectories,
                attachmentsRootDirectory,
                TrustedPathFailureMessage,
                out stream,
                out trustedPath))
        {
            _logger.LogWarning(
                "Managed panel attachment {AttachmentId} is missing or outside trusted storage.",
                attachment.Id);

            return false;
        }

        if (stream is not null && trustedPath is not null)
        {
            return true;
        }

        stream?.Dispose();
        stream = null;
        trustedPath = null;
        _logger.LogWarning(
            "Managed panel attachment {AttachmentId} could not be opened.",
            attachment.Id);

        return false;
    }

    private string GetRelativeAttachmentPath(string panelId, string internalFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(internalFileName);
        string safePanelKey = _keyEncoder.Encode(panelId);

        if (!SafeFileName.IsValid(internalFileName) || !SafeFileName.IsValid(safePanelKey))
        {
            throw new IOException("Panel attachment paths must contain safe file names.");
        }

        return Path.Combine(safePanelKey, internalFileName);
    }

    private static string CreateSafeDisplayFileName(string fileName, string extension)
    {
        string[] segments = fileName.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
            '\\',
            '/');
        string safeName = segments.LastOrDefault() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = string.Concat("attachment", extension);
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidChar, '_');
        }

        return safeName;
    }

    private void DeleteFileIfExists(string path)
    {
        try
        {
            string attachmentsRootDirectory = GetAttachmentsRootDirectory();

            if (!TrustedPathGuard.IsInsideDirectory(attachmentsRootDirectory, path))
            {
                throw new IOException(TrustedPathFailureMessage);
            }

            TrustedPathGuard.EnsureTrustedWriteTarget(
                attachmentsRootDirectory,
                path,
                TrustedPathFailureMessage);

            FileDeletion.DeleteIfExists(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to delete managed panel attachment.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied while deleting managed panel attachment.");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Managed panel attachment path is not supported during deletion.");
        }
    }

    private string GetAttachmentsRootDirectory()
    {
        return Path.GetFullPath(_pathProvider.StateAttachmentsDirectory);
    }
}
