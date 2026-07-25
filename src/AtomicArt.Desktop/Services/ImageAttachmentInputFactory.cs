using Avalonia.Media.Imaging;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

internal static class ImageAttachmentInputFactory
{
    private const string TransferredImageTooLargeMessage =
        "Transferred image exceeds the safe input size limit.";

    public static ImageAttachmentInput CreateEncoded(
        string fileNamePrefix,
        ImageDataTransferFormatDescriptor format,
        byte[] content,
        int maxInputBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileNamePrefix);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);

        if (content.LongLength > maxInputBytes)
        {
            throw new InvalidDataException(TransferredImageTooLargeMessage);
        }

        AttachedImageDto image = new(
            string.Concat(fileNamePrefix, format.Extension),
            format.ContentType,
            content);

        return ImageAttachmentInput.FromImage(image);
    }

    public static ImageAttachmentInput CreateBitmap(
        string fileName,
        Bitmap bitmap,
        int maxInputBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);

        return new ImageAttachmentInput(
            fileName,
            read: ct => EncodeBitmapAsync(fileName, bitmap, maxInputBytes, ct),
            ownedResource: bitmap);
    }

    private static async Task<AttachedImageDto?> EncodeBitmapAsync(
        string fileName,
        Bitmap bitmap,
        int maxInputBytes,
        CancellationToken ct)
    {
        return await Task.Run(
                () => EncodeBitmap(fileName, bitmap, maxInputBytes, ct),
                ct)
            .ConfigureAwait(false);
    }

    private static AttachedImageDto EncodeBitmap(
        string fileName,
        Bitmap bitmap,
        int maxInputBytes,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using LimitedMemoryStream stream = new(maxInputBytes);

        try
        {
            bitmap.Save(stream);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidDataException(
                TransferredImageTooLargeMessage,
                ex);
        }

        ct.ThrowIfCancellationRequested();

        return new AttachedImageDto(
            fileName,
            GenerationImageContentTypes.Png,
            stream.ToArray());
    }
}
