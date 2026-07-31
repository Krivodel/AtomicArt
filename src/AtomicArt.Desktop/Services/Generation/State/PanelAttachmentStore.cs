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

    public PanelAttachmentStore(
        IAtomicArtDataPathProvider pathProvider,
        IStatePathKeyEncoder keyEncoder,
        IGenerationImageFormatRegistry formatRegistry,
        IDataRootAccessCoordinator accessCoordinator,
        TrustedFileStreamFactory trustedFileStreamFactory,
        ILogger<PanelAttachmentStore> logger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _keyEncoder = keyEncoder ?? throw new ArgumentNullException(nameof(keyEncoder));
        _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
        _accessCoordinator = accessCoordinator
            ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _trustedFileStreamFactory = trustedFileStreamFactory
            ?? throw new ArgumentNullException(nameof(trustedFileStreamFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        using DataRootAccessLease accessLease =
            await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);

        if (!PanelAttachmentStateSanitizer.IsValid(attachment))
        {
            throw new InvalidOperationException("Panel attachment state is invalid.");
        }

        if (!string.Equals(attachment.ContentType, image.ContentType, StringComparison.OrdinalIgnoreCase)
            || attachment.SizeBytes != image.Content.LongLength)
        {
            throw new InvalidOperationException("Panel attachment state does not match attachment content.");
        }

        string path = GetAttachmentPath(panelId, attachment.InternalFileName);
        string panelDirectory = Path.GetDirectoryName(path)
            ?? throw new IOException(TrustedPathFailureMessage);
        bool attachmentFileSaved = false;

        EnsurePanelDirectory(panelDirectory);
        TrustedPathGuard.EnsureTrustedWriteTarget(
            panelDirectory,
            path,
            TrustedPathFailureMessage);

        try
        {
            await using (FileStream stream = _trustedFileStreamFactory.CreateNewFileForWrite(
                panelDirectory,
                path,
                TrustedPathFailureMessage))
            {
                await stream.WriteAsync(image.Content, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }

            attachmentFileSaved = true;
            _logger.LogInformation(
                "Managed panel attachment {AttachmentId} saved with {SizeBytes} bytes.",
                attachment.Id,
                attachment.SizeBytes);
        }
        finally
        {
            if (!attachmentFileSaved)
            {
                DeleteFileIfExists(path);
            }
        }
    }

    public async Task<AttachedImageDto?> LoadAsync(
        string panelId,
        PanelAttachmentState attachment,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        ArgumentNullException.ThrowIfNull(attachment);
        using DataRootAccessLease accessLease =
            await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);

        try
        {
            if (!TryOpenAttachmentFileForRead(
                    panelId,
                    attachment,
                    out FileStream? stream,
                    out string? _)
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

                return new AttachedImageDto(
                    attachment.FileName,
                    attachment.ContentType,
                    content);
            }
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
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        ArgumentNullException.ThrowIfNull(attachment);
        using DataRootAccessLease accessLease =
            await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);

        try
        {
            if (!TryOpenAttachmentFileForRead(
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
        ArgumentException.ThrowIfNullOrWhiteSpace(internalFileName);

        if (!SafeFileName.IsValid(internalFileName))
        {
            throw new IOException("Panel attachment internal file name must be a safe file name.");
        }

        return Path.GetFullPath(Path.Combine(GetPanelDirectory(panelId), internalFileName));
    }

    private bool TryOpenAttachmentFileForRead(
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

    private string GetPanelDirectory(string panelId)
    {
        string attachmentsRootDirectory = GetAttachmentsRootDirectory();
        string safePanelKey = _keyEncoder.Encode(panelId);
        string panelDirectory = Path.GetFullPath(Path.Combine(
            attachmentsRootDirectory,
            safePanelKey));

        TrustedPathGuard.EnsureInsideDirectory(
            attachmentsRootDirectory,
            panelDirectory,
            TrustedPathFailureMessage);

        return panelDirectory;
    }

    private void EnsurePanelDirectory(string panelDirectory)
    {
        TrustedPathGuard.EnsureTrustedDirectoryExists(
            _pathProvider,
            _pathProvider.StateDirectory,
            TrustedPathFailureMessage);
        TrustedPathGuard.EnsureTrustedDirectoryExists(
            _pathProvider,
            GetAttachmentsRootDirectory(),
            TrustedPathFailureMessage);
        TrustedPathGuard.EnsureTrustedDirectoryExists(
            panelDirectory,
            directory => Directory.CreateDirectory(directory),
            TrustedPathFailureMessage);
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
