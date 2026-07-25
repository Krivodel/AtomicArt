using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services.Windows;

internal sealed class WindowsVirtualFileReader
{
    private const string DroppedImageFileName = "dropped-image";
    private const string IncompleteVirtualFileMessage =
        "The virtual file could not be read from the drag-and-drop source.";
    private const string VirtualFileTooLargeMessage =
        "The virtual file exceeds the safe input size limit.";
    private const int MaximumDescriptorBytes = 64 * 1024;

    private static readonly Lazy<short> AnsiDescriptorFormat =
        new(() => RegisterFormat(VirtualFileDataTransferFormats.AnsiDescriptor));
    private static readonly Lazy<short> UnicodeDescriptorFormat =
        new(() => RegisterFormat(VirtualFileDataTransferFormats.UnicodeDescriptor));
    private static readonly Lazy<short> ContentsFormat =
        new(() => RegisterFormat(VirtualFileDataTransferFormats.Contents));

    private readonly AttachedImageFileReader _imageFileReader;
    private readonly ILogger<WindowsVirtualFileReader> _logger;

    public WindowsVirtualFileReader(
        AttachedImageFileReader imageFileReader,
        ILogger<WindowsVirtualFileReader> logger)
    {
        ArgumentNullException.ThrowIfNull(imageFileReader);
        ArgumentNullException.ThrowIfNull(logger);

        _imageFileReader = imageFileReader;
        _logger = logger;
    }

    [SupportedOSPlatform("windows")]
    public IReadOnlyList<ImageAttachmentInput> ReadInputs(
        IDataObject dataObject,
        int maxInputBytes)
    {
        ArgumentNullException.ThrowIfNull(dataObject);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);

        try
        {
            if (!TryReadDescriptors(
                    dataObject,
                    out IReadOnlyList<WindowsVirtualFileDescriptor> descriptors))
            {
                return Array.Empty<ImageAttachmentInput>();
            }

            List<ImageAttachmentInput> inputs = [];

            for (int index = 0; index < descriptors.Count; index++)
            {
                WindowsVirtualFileDescriptor descriptor = descriptors[index];

                if (!descriptor.IsDirectory)
                {
                    inputs.Add(ReadInput(
                        dataObject,
                        descriptor,
                        index,
                        maxInputBytes));
                }
            }

            _logger.LogInformation(
                "Virtual file drop produced {AttachmentCount} buffered attachment inputs.",
                inputs.Count);

            return inputs;
        }
        catch (Exception ex) when (IsRecoverableReadFailure(ex))
        {
            LogReadFailure(ex, null);

            return new ImageAttachmentInput[]
            {
                ImageAttachmentInput.FromError(
                    DroppedImageFileName,
                    new InvalidDataException(
                        IncompleteVirtualFileMessage,
                        ex))
            };
        }
    }

    [SupportedOSPlatform("windows")]
    private ImageAttachmentInput ReadInput(
        IDataObject dataObject,
        WindowsVirtualFileDescriptor descriptor,
        int index,
        int maxInputBytes)
    {
        string fileName = CreateFileName(descriptor.FileName, index);

        try
        {
            if (descriptor.DeclaredSize > (ulong)maxInputBytes)
            {
                throw new InvalidDataException(VirtualFileTooLargeMessage);
            }

            byte[] content = WindowsStorageMediumReader.ReadIndexedContent(
                dataObject,
                ContentsFormat.Value,
                index,
                maxInputBytes,
                VirtualFileTooLargeMessage);

            return _imageFileReader.CreateBufferedInput(fileName, content);
        }
        catch (Exception ex) when (IsRecoverableReadFailure(ex))
        {
            LogReadFailure(ex, index);
            Exception inputError = ex is InvalidDataException
                ? ex
                : new InvalidDataException(IncompleteVirtualFileMessage, ex);

            return ImageAttachmentInput.FromError(fileName, inputError);
        }
    }

    private static bool TryReadDescriptors(
        IDataObject dataObject,
        out IReadOnlyList<WindowsVirtualFileDescriptor> descriptors)
    {
        if (WindowsStorageMediumReader.TryReadGlobalMemory(
                dataObject,
                UnicodeDescriptorFormat.Value,
                MaximumDescriptorBytes,
                "The virtual file descriptor is too large.",
                out byte[] unicodeData))
        {
            descriptors = WindowsVirtualFileDescriptorParser.Parse(
                unicodeData,
                isUnicode: true);
            return true;
        }

        if (WindowsStorageMediumReader.TryReadGlobalMemory(
                dataObject,
                AnsiDescriptorFormat.Value,
                MaximumDescriptorBytes,
                "The virtual file descriptor is too large.",
                out byte[] ansiData))
        {
            descriptors = WindowsVirtualFileDescriptorParser.Parse(
                ansiData,
                isUnicode: false);
            return true;
        }

        descriptors = Array.Empty<WindowsVirtualFileDescriptor>();
        return false;
    }

    private static short RegisterFormat(string name)
    {
        uint formatId = WindowsNativeDragDrop.RegisterClipboardFormat(name);

        if (formatId == 0 || formatId > ushort.MaxValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return unchecked((short)formatId);
    }

    private static string CreateFileName(string sourceName, int index)
    {
        string normalizedPath = sourceName.Replace('/', '\\');
        string candidate = Path.GetFileName(normalizedPath);

        return TransferredImageFileName.Sanitize(
            candidate,
            string.Concat(DroppedImageFileName, "-", index + 1));
    }

    private static bool IsRecoverableReadFailure(Exception ex)
    {
        return ex is ExternalException
            or InvalidDataException
            or IOException
            or OverflowException;
    }

    private void LogReadFailure(Exception ex, int? itemIndex)
    {
        _logger.LogWarning(
            "Virtual file drop read failed for item index {ItemIndex} with error type {ErrorType} and HRESULT {HResult}.",
            itemIndex,
            ex.GetType().Name,
            ex.HResult);
    }
}
