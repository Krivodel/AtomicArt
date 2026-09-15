using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Windows;

internal sealed class WindowsClipboardImageReader : IPlatformClipboardImageReader
{
    private const uint DibFormat = 8;
    private const uint DibV5Format = 17;
    private const uint FileDropFormat = 15;
    private const uint FileCountIndex = uint.MaxValue;
    private const int ClipboardOpenAttempts = 4;
    private const int ClipboardRetryDelayMilliseconds = 10;

    private readonly AttachedImageFileReader _fileReader;

    public WindowsClipboardImageReader(AttachedImageFileReader fileReader)
    {
        _fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
    }

    public async Task<ImageAttachmentInput?> TryGetImageAsync(
        int maxInputBytes,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
        ct.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        uint[] pngFormatIds = GetPngFormatIds();

        for (int attempt = 0; attempt < ClipboardOpenAttempts; attempt++)
        {
            if (WindowsNativeClipboard.OpenClipboard(nint.Zero))
            {
                byte[]? pngContent;
                string? filePath;
                byte[]? dibContent;
                try
                {
                    pngContent = TryReadPng(pngFormatIds, maxInputBytes);
                    filePath = pngContent is null
                        ? TryReadFilePath()
                        : null;
                    dibContent = pngContent is null && filePath is null
                        ? TryReadDib(maxInputBytes)
                        : null;
                }
                finally
                {
                    _ = WindowsNativeClipboard.CloseClipboard();
                }

                if (pngContent is not null)
                {
                    return CreatePngInput(pngContent);
                }

                if (filePath is not null)
                {
                    return _fileReader.CreateInput(filePath, maxInputBytes);
                }

                if (dibContent is null)
                {
                    return null;
                }

                return await Task.Run(
                        () => CreateDibInput(dibContent, maxInputBytes),
                        ct)
                    .ConfigureAwait(false);
            }

            if (attempt + 1 < ClipboardOpenAttempts)
            {
                await Task.Delay(ClipboardRetryDelayMilliseconds, ct)
                    .ConfigureAwait(false);
            }
        }

        throw new Win32Exception(
            Marshal.GetLastWin32Error(),
            "The Windows clipboard could not be opened.");
    }

    private static uint[] GetPngFormatIds()
    {
        ImageDataTransferFormatDescriptor pngFormat = ImageDataTransferFormats
            .EncodedImages
            .Single(format => string.Equals(
                format.ContentType,
                GenerationImageContentTypes.Png,
                StringComparison.Ordinal));
        uint[] formatIds = pngFormat.Formats
            .Select(format => WindowsNativeDragDrop.RegisterClipboardFormat(
                format.Identifier))
            .Distinct()
            .ToArray();

        if (formatIds.Length == 0 || formatIds.Contains(0u))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The Windows PNG clipboard formats could not be registered.");
        }

        return formatIds;
    }

    private static byte[]? TryReadPng(
        IReadOnlyList<uint> formatIds,
        int maxInputBytes)
    {
        foreach (uint formatId in formatIds)
        {
            if (!WindowsNativeClipboard.IsClipboardFormatAvailable(formatId))
            {
                continue;
            }

            nint memoryHandle = WindowsNativeClipboard.GetClipboardData(formatId);

            if (memoryHandle != nint.Zero)
            {
                return WindowsStorageMediumReader.ReadGlobalMemory(
                    memoryHandle,
                    maxInputBytes,
                    "The clipboard PNG exceeds the safe input size limit.");
            }
        }

        return null;
    }

    private static string? TryReadFilePath()
    {
        if (!WindowsNativeClipboard.IsClipboardFormatAvailable(FileDropFormat))
        {
            return null;
        }

        nint dropHandle = GetClipboardHandle(
            FileDropFormat,
            "The Windows clipboard file list is unavailable.");
        uint fileCount = WindowsNativeClipboard.DragQueryFile(
            dropHandle,
            FileCountIndex,
            filePath: null,
            characterCount: 0);

        if (fileCount == 0)
        {
            return null;
        }

        uint pathLength = WindowsNativeClipboard.DragQueryFile(
            dropHandle,
            fileIndex: 0,
            filePath: null,
            characterCount: 0);

        if (pathLength == 0 || pathLength >= short.MaxValue)
        {
            return null;
        }

        StringBuilder filePath = new(checked((int)pathLength + 1));
        uint copiedLength = WindowsNativeClipboard.DragQueryFile(
            dropHandle,
            fileIndex: 0,
            filePath,
            checked(pathLength + 1));

        return copiedLength == pathLength
            ? filePath.ToString()
            : null;
    }

    private static byte[]? TryReadDib(int maxInputBytes)
    {
        uint formatId = WindowsNativeClipboard.IsClipboardFormatAvailable(DibV5Format)
            ? DibV5Format
            : WindowsNativeClipboard.IsClipboardFormatAvailable(DibFormat)
                ? DibFormat
                : 0;

        if (formatId == 0)
        {
            return null;
        }

        nint memoryHandle = GetClipboardHandle(
            formatId,
            "The Windows clipboard bitmap is unavailable.");

        return WindowsStorageMediumReader.ReadGlobalMemory(
            memoryHandle,
            WindowsClipboardDibCodec.GetMaximumDibBytes(maxInputBytes),
            "The clipboard bitmap exceeds the safe memory limit.");
    }

    private static ImageAttachmentInput CreateDibInput(
        byte[] dibContent,
        int maxInputBytes)
    {
        byte[] pngContent = WindowsClipboardDibCodec.ConvertToPng(
            dibContent,
            maxInputBytes);
        AttachedImageDto image = new(
            "clipboard.png",
            GenerationImageContentTypes.Png,
            pngContent);

        return ImageAttachmentInput.FromImage(image);
    }

    private static ImageAttachmentInput CreatePngInput(byte[] pngContent)
    {
        AttachedImageDto image = new(
            "clipboard.png",
            GenerationImageContentTypes.Png,
            pngContent);

        return ImageAttachmentInput.FromImage(image);
    }

    private static nint GetClipboardHandle(uint formatId, string unavailableMessage)
    {
        nint handle = WindowsNativeClipboard.GetClipboardData(formatId);

        if (handle == nint.Zero)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                unavailableMessage);
        }

        return handle;
    }
}
