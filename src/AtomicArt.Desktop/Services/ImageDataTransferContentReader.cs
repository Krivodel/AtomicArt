using Avalonia.Input;

namespace AtomicArt.Desktop.Services;

internal static class ImageDataTransferContentReader
{
    public static TransferredImageContent? TryGetEncodedImage(
        IDataTransfer dataTransfer)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        foreach (ImageDataTransferFormatDescriptor descriptor
                 in ImageDataTransferFormats.EncodedImages)
        {
            foreach (DataFormat<byte[]> format in descriptor.Formats)
            {
                byte[]? content = dataTransfer.TryGetValue(format);

                if (content is not null)
                {
                    return new TransferredImageContent(descriptor, content);
                }
            }
        }

        return TryGetOtherMimeImage(dataTransfer);
    }

    public static async Task<TransferredImageContent?> TryGetEncodedImageAsync(
        IAsyncDataTransfer dataTransfer)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        foreach (ImageDataTransferFormatDescriptor descriptor
                 in ImageDataTransferFormats.EncodedImages)
        {
            foreach (DataFormat<byte[]> format in descriptor.Formats)
            {
                byte[]? content = await dataTransfer
                    .TryGetValueAsync(format)
                    .ConfigureAwait(false);

                if (content is not null)
                {
                    return new TransferredImageContent(descriptor, content);
                }
            }
        }

        return await TryGetOtherMimeImageAsync(dataTransfer)
            .ConfigureAwait(false);
    }

    private static TransferredImageContent? TryGetOtherMimeImage(
        IDataTransfer dataTransfer)
    {
        foreach (IDataTransferItem item in dataTransfer.Items)
        {
            foreach (DataFormat format in item.Formats)
            {
                if (!IsOtherImageMimeFormat(format))
                {
                    continue;
                }

                if (item.TryGetRaw(format) is byte[] content)
                {
                    return CreateOtherMimeContent(format.Identifier, content);
                }
            }
        }

        return null;
    }

    private static async Task<TransferredImageContent?> TryGetOtherMimeImageAsync(
        IAsyncDataTransfer dataTransfer)
    {
        foreach (IAsyncDataTransferItem item in dataTransfer.Items)
        {
            foreach (DataFormat format in item.Formats)
            {
                if (!IsOtherImageMimeFormat(format))
                {
                    continue;
                }

                object? value = await item
                    .TryGetRawAsync(format)
                    .ConfigureAwait(false);

                if (value is byte[] content)
                {
                    return CreateOtherMimeContent(format.Identifier, content);
                }
            }
        }

        return null;
    }

    private static bool IsOtherImageMimeFormat(DataFormat format)
    {
        return ImageDataTransferFormats.TryNormalizeImageMimeType(
                format.Identifier,
                out string _)
            && !ImageDataTransferFormats.EncodedImages.Any(descriptor =>
                descriptor.Formats.Contains(format));
    }

    private static TransferredImageContent CreateOtherMimeContent(
        string contentType,
        byte[] content)
    {
        if (!ImageDataTransferFormats.TryNormalizeImageMimeType(
                contentType,
                out string normalizedContentType))
        {
            throw new ArgumentException(
                "Image content type must be a valid MIME type.",
                nameof(contentType));
        }

        ImageDataTransferFormatDescriptor descriptor = new(
            normalizedContentType,
            ".img",
            [DataFormat.CreateBytesPlatformFormat(normalizedContentType)]);

        return new TransferredImageContent(descriptor, content);
    }
}

internal sealed record TransferredImageContent(
    ImageDataTransferFormatDescriptor Format,
    byte[] Content);
