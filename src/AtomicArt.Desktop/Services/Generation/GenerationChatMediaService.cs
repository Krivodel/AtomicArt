using Microsoft.Extensions.Options;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationChatMediaService
{
    public int MaxInputImageBytes => _options.Value.MaxInputImageBytes;

    private readonly IGenerationReferenceStore _references;
    private readonly IFilePickerService _picker;
    private readonly IAttachedImagePreparationService _preparation;
    private readonly IImageViewerService _viewer;
    private readonly IDataRootAccessCoordinator _access;
    private readonly IAtomicArtDataPathProvider _paths;
    private readonly TrustedFileStreamFactory _streams;
    private readonly IGenerationImageFormatRegistry _formats;
    private readonly IOptions<GenerationClientOptions> _options;

    public GenerationChatMediaService(
        IGenerationReferenceStore references,
        IFilePickerService picker,
        IAttachedImagePreparationService preparation,
        IImageViewerService viewer,
        IDataRootAccessCoordinator access,
        IAtomicArtDataPathProvider paths,
        TrustedFileStreamFactory streams,
        IGenerationImageFormatRegistry formats,
        IOptions<GenerationClientOptions> options)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
        _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _streams = streams ?? throw new ArgumentNullException(nameof(streams));
        _formats = formats ?? throw new ArgumentNullException(nameof(formats));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<AttachedImageDto?> ReadResultAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Task.Run(() => ReadResultCoreAsync(path, ct), ct);
    }

    public async Task<IReadOnlyList<AttachedImageDto>> PickAsync(
        ImageModelOption model, int availableSlots, long availableBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);
        IReadOnlyList<ImageAttachmentInput> inputs = await _picker.PickImagesAsync(
            _options.Value.MaxInputImageBytes, ct).ConfigureAwait(false);

        return await PrepareAsync(model, inputs, availableSlots, availableBytes, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AttachedImageDto>> PrepareAsync(
        ImageModelOption model, IReadOnlyList<ImageAttachmentInput> inputs,
        int availableSlots, long availableBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(inputs);

        try
        {
            List<AttachedImageDto> images = [];

            foreach (ImageAttachmentInput input in inputs.Take(availableSlots))
            {
                AttachedImageDto? image = await input.PrepareAsync(_preparation, model, ct).ConfigureAwait(false);

                if (image is not null && image.Content.Length <= availableBytes)
                {
                    availableBytes -= image.Content.Length;
                    images.Add(image);
                }
            }

            return images;
        }
        finally
        {
            foreach (ImageAttachmentInput input in inputs)
            {
                input.Dispose();
            }
        }
    }

    public Task OpenAsync(IReadOnlyList<GenerationReference> images, int selectedIndex, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentOutOfRangeException.ThrowIfNegative(selectedIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(selectedIndex, images.Count);

        GalleryImageViewerItem[] items = images.Select(image =>
            new GalleryImageViewerItem(Guid.NewGuid(), new GalleryReferenceImageViewerSource(image))).ToArray();
        GalleryImageViewerRequest request = new(
            new GalleryStaticImageViewerItemsSource(items), items[selectedIndex].Id, AttachImagesCommand: null);

        return _viewer.OpenAsync(request, ct);
    }

    internal async Task<long> GetTotalReferenceBytesAsync(IReadOnlyList<GenerationReference> images, CancellationToken ct)
    {
        long bytes = 0L;

        foreach (GenerationReference image in images)
        {
            ReferenceSize? size = await _references.ReadAsync<ReferenceSize>(
                image, (stream, _) => Task.FromResult<ReferenceSize?>(new ReferenceSize(stream.Length)), ct)
                .ConfigureAwait(false);
            bytes += size?.Bytes ?? 0L;
        }

        return bytes;
    }

    private async Task<AttachedImageDto?> ReadResultCoreAsync(string path, CancellationToken ct)
    {
        using DataRootAccessLease lease = await _access.AcquireAccessAsync(ct).ConfigureAwait(false);

        if (!_formats.TryGetByFileName(path, out IGenerationImageFormat? format)
            || format is null
            || !_streams.TryOpenExistingFileForRead(path,
                    new string[] { _paths.ArtDirectory, GenerationReferenceFilePolicy.GetDirectory(_paths) }, _paths.RootDirectory,
                    "The conversation image path is unsafe.", out FileStream? stream, out _)
            || stream is null)
        {
            return null;
        }

        await using (stream.ConfigureAwait(false))
        {
            if (stream.Length <= 0 || stream.Length > _options.Value.MaxInputImageBytes || stream.Length > Array.MaxLength)
            {
                return null;
            }

            byte[] bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, ct).ConfigureAwait(false);

            if (!format.MatchesSignature(bytes))
            {
                return null;
            }

            return new AttachedImageDto(Path.GetFileName(path), format.ContentType, bytes);
        }
    }

    private sealed record ReferenceSize(long Bytes);
}
