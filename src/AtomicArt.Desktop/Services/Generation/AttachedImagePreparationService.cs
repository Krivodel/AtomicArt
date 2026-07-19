using System.Diagnostics;

using Microsoft.Extensions.Logging;
using SkiaSharp;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class AttachedImagePreparationService :
    IAttachedImagePreparationService,
    IGenerationModelService
{
    private const int MinimumLossyQuality = 35;
    private const int MaximumLossyQuality = 100;
    private const int LossyQualitySearchSteps = 6;
    private const int MaximumResizeAttempts = 6;
    private const double MaximumLosslessCandidateRatio = 1.05d;
    private const string PreparedFileNameSuffix = "-prepared";

    private readonly IAttachedImageSignatureValidator _signatureValidator;
    private readonly IGenerationImageFormatRegistry _formatRegistry;
    private readonly IAttachedImageCodec _codec;
    private readonly AttachedImagePreparationConcurrencyLimiter _concurrencyLimiter;
    private readonly ILogger<AttachedImagePreparationService> _logger;

    public AttachedImagePreparationService(
        IAttachedImageSignatureValidator signatureValidator,
        IGenerationImageFormatRegistry formatRegistry,
        IAttachedImageCodec codec,
        AttachedImagePreparationConcurrencyLimiter concurrencyLimiter,
        ILogger<AttachedImagePreparationService> logger)
    {
        ArgumentNullException.ThrowIfNull(signatureValidator);
        ArgumentNullException.ThrowIfNull(formatRegistry);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(concurrencyLimiter);
        ArgumentNullException.ThrowIfNull(logger);

        _signatureValidator = signatureValidator;
        _formatRegistry = formatRegistry;
        _codec = codec;
        _concurrencyLimiter = concurrencyLimiter;
        _logger = logger;
    }

    public async Task<AttachedImageDto?> PrepareAsync(
        AttachedImageDto image,
        ImageModelOption selectedModel,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(selectedModel);
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Attached image preparation started. ContentType: {ContentType}, SourceBytes: {SourceBytes}, MaxBytes: {MaxBytes}",
            image.ContentType,
            image.Content.LongLength,
            selectedModel.MaxAttachedImageBytes);

        if (IsAlreadyAccepted(image, selectedModel))
        {
            _logger.LogInformation(
                "Attached image preparation completed without conversion. OutputBytes: {OutputBytes}, ElapsedMilliseconds: {ElapsedMilliseconds}",
                image.Content.LongLength,
                totalStopwatch.ElapsedMilliseconds);

            return image;
        }

        AttachedImageCodecInfo? imageInfo = await Task.Run(
                () => _codec.ReadInfo(image.Content),
                ct)
            .ConfigureAwait(false);

        if (imageInfo is null
            || imageInfo.Width <= 0
            || imageInfo.Height <= 0)
        {
            _logger.LogWarning(
                "Attached image metadata could not be read. ElapsedMilliseconds: {ElapsedMilliseconds}",
                totalStopwatch.ElapsedMilliseconds);

            return null;
        }

        _logger.LogInformation(
            "Attached image metadata read. Width: {Width}, Height: {Height}, AlphaType: {AlphaType}",
            imageInfo.Width,
            imageInfo.Height,
            imageInfo.AlphaType);
        await _concurrencyLimiter.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            AttachedImageDto? result = await Task.Run(
                    () => Prepare(image, selectedModel, imageInfo, ct),
                    ct)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Attached image preparation completed. Result: {Result}, OutputContentType: {OutputContentType}, OutputBytes: {OutputBytes}, ElapsedMilliseconds: {ElapsedMilliseconds}",
                result is null ? "Rejected" : "Accepted",
                result?.ContentType,
                result?.Content.LongLength,
                totalStopwatch.ElapsedMilliseconds);

            return result;
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private AttachedImageDto? Prepare(
        AttachedImageDto image,
        ImageModelOption selectedModel,
        AttachedImageCodecInfo imageInfo,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        SKSizeI sourceSize = new(imageInfo.Width, imageInfo.Height);
        Stopwatch decodeStopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Attached image decoding started. Width: {Width}, Height: {Height}",
            imageInfo.Width,
            imageInfo.Height);
        using SKBitmap? sourceBitmap = _codec.Decode(image.Content, ct);

        if (sourceBitmap is null)
        {
            _logger.LogWarning(
                "Attached image decoding failed. ElapsedMilliseconds: {ElapsedMilliseconds}",
                decodeStopwatch.ElapsedMilliseconds);

            return null;
        }

        _logger.LogInformation(
            "Attached image decoded. Width: {Width}, Height: {Height}, ElapsedMilliseconds: {ElapsedMilliseconds}",
            sourceBitmap.Width,
            sourceBitmap.Height,
            decodeStopwatch.ElapsedMilliseconds);
        int maximumDimension = ResolveLosslessFormat(selectedModel)
                               == AttachedImageEncodingFormat.Webp
            ? AttachedImagePreparationPlanner.MaximumWebpDimension
            : int.MaxValue;
        SKSizeI formatWorkingSize = AttachedImagePreparationPlanner.CalculateInitialWorkingSize(
            imageInfo,
            maximumDimension);
        AttachedImagePreparationProbeResult? probeResult = CreateEncodingProbe(
            sourceBitmap,
            imageInfo,
            formatWorkingSize,
            selectedModel,
            ct);
        SKSizeI workingSize = probeResult?.WorkingSize ?? formatWorkingSize;
        bool shouldTryLossless =
            workingSize.Width == imageInfo.Width
            && workingSize.Height == imageInfo.Height
            && (probeResult?.IsLosslessEncodingPromising ?? true);

        if (workingSize == sourceSize)
        {
            _logger.LogInformation(
                "Attached image will be encoded at source resolution. Width: {Width}, Height: {Height}, TryLossless: {TryLossless}",
                workingSize.Width,
                workingSize.Height,
                shouldTryLossless);

            return PrepareEncodedImage(
                image,
                sourceBitmap,
                selectedModel,
                shouldTryLossless,
                ct);
        }

        Stopwatch resizeStopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Attached image resize started. SourceWidth: {SourceWidth}, SourceHeight: {SourceHeight}, TargetWidth: {TargetWidth}, TargetHeight: {TargetHeight}",
            imageInfo.Width,
            imageInfo.Height,
            workingSize.Width,
            workingSize.Height);
        using SKBitmap workingBitmap = _codec.Resize(sourceBitmap, workingSize);
        _logger.LogInformation(
            "Attached image resize completed. TargetWidth: {TargetWidth}, TargetHeight: {TargetHeight}, ElapsedMilliseconds: {ElapsedMilliseconds}",
            workingSize.Width,
            workingSize.Height,
            resizeStopwatch.ElapsedMilliseconds);

        return PrepareEncodedImage(
            image,
            workingBitmap,
            selectedModel,
            false,
            ct);
    }

    private AttachedImageDto? PrepareEncodedImage(
        AttachedImageDto image,
        SKBitmap sourceBitmap,
        ImageModelOption selectedModel,
        bool shouldTryLossless,
        CancellationToken ct)
    {
        LosslessEncodingResult? losslessResult = shouldTryLossless
            ? TryEncodeLosslessly(
                image.FileName,
                sourceBitmap,
                selectedModel,
                ct)
            : null;

        if (losslessResult?.AcceptedImage is not null)
        {
            return losslessResult.AcceptedImage;
        }

        AttachedImageEncodingFormat? lossyFormat = ResolveLossyFormat(
            selectedModel,
            sourceBitmap.AlphaType);

        if (lossyFormat is not null)
        {
            LossyEncodingResult? lossyResult = FindHighestQualityEncoding(
                sourceBitmap,
                lossyFormat.Value,
                selectedModel.MaxAttachedImageBytes,
                ct);

            if (lossyResult is null)
            {
                return null;
            }

            if (lossyResult.BestBytes is not null)
            {
                return CreatePreparedImage(image.FileName, lossyFormat.Value, lossyResult.BestBytes);
            }

            return TryResizeToFit(
                image.FileName,
                sourceBitmap,
                selectedModel,
                lossyFormat.Value,
                lossyResult.MinimumQualityBytes,
                ct);
        }

        return PreparePng(
            image.FileName,
            sourceBitmap,
            selectedModel,
            losslessResult,
            ct);
    }

    private AttachedImagePreparationProbeResult? CreateEncodingProbe(
        SKBitmap sourceBitmap,
        AttachedImageCodecInfo imageInfo,
        SKSizeI formatWorkingSize,
        ImageModelOption selectedModel,
        CancellationToken ct)
    {
        if (!AttachedImagePreparationPlanner.ShouldUseEncodingProbe(imageInfo))
        {
            return null;
        }

        SKSizeI probeSize =
            AttachedImagePreparationPlanner.CalculateEncodingProbeSize(imageInfo);
        Stopwatch probeStopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Attached image encoding probe started. SourceWidth: {SourceWidth}, SourceHeight: {SourceHeight}, ProbeWidth: {ProbeWidth}, ProbeHeight: {ProbeHeight}",
            imageInfo.Width,
            imageInfo.Height,
            probeSize.Width,
            probeSize.Height);
        using SKBitmap probeBitmap = _codec.Resize(sourceBitmap, probeSize);
        bool sourceFitsFormat =
            formatWorkingSize.Width == imageInfo.Width
            && formatWorkingSize.Height == imageInfo.Height;
        AttachedImageEncodingFormat? losslessFormat = ResolveLosslessFormat(selectedModel);
        byte[]? losslessProbeBytes = null;

        if (sourceFitsFormat && losslessFormat is not null)
        {
            losslessProbeBytes = _codec.EncodeLosslessly(
                probeBitmap,
                losslessFormat.Value,
                AttachedImageCompressionEffort.Fast,
                ct);

            if (losslessProbeBytes is not null)
            {
                long estimatedLosslessBytes =
                    AttachedImagePreparationPlanner.EstimateEncodedBytes(
                        formatWorkingSize,
                        probeSize,
                        losslessProbeBytes.LongLength);
                _logger.LogInformation(
                    "Lossless attachment probe encoded. Format: {Format}, ProbeBytes: {ProbeBytes}, EstimatedBytes: {EstimatedBytes}, ElapsedMilliseconds: {ElapsedMilliseconds}",
                    losslessFormat.Value,
                    losslessProbeBytes.LongLength,
                    estimatedLosslessBytes,
                    probeStopwatch.ElapsedMilliseconds);

                if (IsCloseToLimit(
                        estimatedLosslessBytes,
                        selectedModel.MaxAttachedImageBytes))
                {
                    return new AttachedImagePreparationProbeResult(
                        formatWorkingSize,
                        true);
                }
            }
        }

        AttachedImageEncodingFormat? lossyFormat = ResolveLossyFormat(
            selectedModel,
            sourceBitmap.AlphaType);

        if (lossyFormat is not null)
        {
            byte[]? lossyProbeBytes = _codec.EncodeWithLoss(
                probeBitmap,
                lossyFormat.Value,
                MaximumLossyQuality,
                ct);

            if (lossyProbeBytes is null)
            {
                return new AttachedImagePreparationProbeResult(
                    formatWorkingSize,
                    false);
            }

            long estimatedLossyBytes =
                AttachedImagePreparationPlanner.EstimateEncodedBytes(
                    formatWorkingSize,
                    probeSize,
                    lossyProbeBytes.LongLength);
            SKSizeI estimatedWorkingSize = CalculateEstimatedWorkingSize(
                formatWorkingSize,
                estimatedLossyBytes,
                selectedModel.MaxAttachedImageBytes);
            _logger.LogInformation(
                "Lossy attachment probe encoded. Format: {Format}, Quality: {Quality}, ProbeBytes: {ProbeBytes}, EstimatedBytes: {EstimatedBytes}, WorkingWidth: {WorkingWidth}, WorkingHeight: {WorkingHeight}, ElapsedMilliseconds: {ElapsedMilliseconds}",
                lossyFormat.Value,
                MaximumLossyQuality,
                lossyProbeBytes.LongLength,
                estimatedLossyBytes,
                estimatedWorkingSize.Width,
                estimatedWorkingSize.Height,
                probeStopwatch.ElapsedMilliseconds);

            return new AttachedImagePreparationProbeResult(
                estimatedWorkingSize,
                false);
        }

        if (losslessProbeBytes is null)
        {
            return new AttachedImagePreparationProbeResult(
                formatWorkingSize,
                false);
        }

        long estimatedBytes = AttachedImagePreparationPlanner.EstimateEncodedBytes(
            formatWorkingSize,
            probeSize,
            losslessProbeBytes.LongLength);

        return new AttachedImagePreparationProbeResult(
            CalculateEstimatedWorkingSize(
                formatWorkingSize,
                estimatedBytes,
                selectedModel.MaxAttachedImageBytes),
            false);
    }

    private AttachedImageDto? PreparePng(
        string fileName,
        SKBitmap bitmap,
        ImageModelOption selectedModel,
        LosslessEncodingResult? losslessResult,
        CancellationToken ct)
    {
        if (!Supports(selectedModel, GenerationImageContentTypes.Png))
        {
            return null;
        }

        byte[]? pngBytes = losslessResult?.Candidate.Format == AttachedImageEncodingFormat.Png
            ? losslessResult.Candidate.Content
            : EncodePngFast(bitmap, ct);

        if (pngBytes is null)
        {
            return null;
        }

        AttachedImageDto? preparedImage = CreatePreparedPngIfFits(
            fileName,
            pngBytes,
            selectedModel.MaxAttachedImageBytes);

        if (preparedImage is not null)
        {
            return preparedImage;
        }

        return TryResizeToFit(
            fileName,
            bitmap,
            selectedModel,
            AttachedImageEncodingFormat.Png,
            pngBytes,
            ct);
    }

    private byte[]? EncodePngFast(SKBitmap bitmap, CancellationToken ct)
    {
        return _codec.EncodeLosslessly(
            bitmap,
            AttachedImageEncodingFormat.Png,
            AttachedImageCompressionEffort.Fast,
            ct);
    }

    private LosslessEncodingResult? TryEncodeLosslessly(
        string fileName,
        SKBitmap bitmap,
        ImageModelOption selectedModel,
        CancellationToken ct)
    {
        AttachedImageEncodingFormat? format = ResolveLosslessFormat(selectedModel);
        if (format is null)
        {
            return null;
        }

        EncodedImageCandidate? fastCandidate = EncodeLosslessCandidate(
            bitmap,
            format.Value,
            AttachedImageCompressionEffort.Fast,
            ct);

        if (fastCandidate is null)
        {
            return null;
        }

        if (fastCandidate.Content.LongLength <= selectedModel.MaxAttachedImageBytes)
        {
            return new LosslessEncodingResult(
                CreatePreparedImage(fileName, fastCandidate.Format, fastCandidate.Content),
                fastCandidate);
        }

        if (!IsCloseToLimit(fastCandidate.Content.LongLength, selectedModel.MaxAttachedImageBytes))
        {
            return new LosslessEncodingResult(null, fastCandidate);
        }

        EncodedImageCandidate? maximumCandidate = EncodeLosslessCandidate(
            bitmap,
            format.Value,
            AttachedImageCompressionEffort.Maximum,
            ct);

        if (maximumCandidate is null)
        {
            return new LosslessEncodingResult(null, fastCandidate);
        }

        AttachedImageDto? acceptedImage =
            maximumCandidate.Content.LongLength <= selectedModel.MaxAttachedImageBytes
                ? CreatePreparedImage(fileName, maximumCandidate.Format, maximumCandidate.Content)
                : null;

        return new LosslessEncodingResult(acceptedImage, maximumCandidate);
    }

    private EncodedImageCandidate? EncodeLosslessCandidate(
        SKBitmap bitmap,
        AttachedImageEncodingFormat format,
        AttachedImageCompressionEffort effort,
        CancellationToken ct)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Lossless attachment encoding started. Format: {Format}, Effort: {Effort}, Width: {Width}, Height: {Height}",
            format,
            effort,
            bitmap.Width,
            bitmap.Height);
        byte[]? content = _codec.EncodeLosslessly(bitmap, format, effort, ct);
        _logger.LogInformation(
            "Lossless attachment encoding completed. Format: {Format}, Effort: {Effort}, OutputBytes: {OutputBytes}, ElapsedMilliseconds: {ElapsedMilliseconds}",
            format,
            effort,
            content?.LongLength,
            stopwatch.ElapsedMilliseconds);

        return content is null
            ? null
            : new EncodedImageCandidate(format, content);
    }

    private LossyEncodingResult? FindHighestQualityEncoding(
        SKBitmap bitmap,
        AttachedImageEncodingFormat format,
        long maxBytes,
        CancellationToken ct)
    {
        byte[]? maximumQualityBytes = EncodeWithLossCandidate(
            bitmap,
            format,
            MaximumLossyQuality,
            ct);

        if (maximumQualityBytes is null)
        {
            return null;
        }

        if (maximumQualityBytes.LongLength <= maxBytes)
        {
            return new LossyEncodingResult(maximumQualityBytes, maximumQualityBytes);
        }

        byte[]? minimumQualityBytes = EncodeWithLossCandidate(
            bitmap,
            format,
            MinimumLossyQuality,
            ct);

        if (minimumQualityBytes is null)
        {
            return null;
        }

        if (minimumQualityBytes.LongLength > maxBytes)
        {
            return new LossyEncodingResult(null, minimumQualityBytes);
        }

        int low = MinimumLossyQuality + 1;
        int high = MaximumLossyQuality - 1;
        byte[] bestBytes = minimumQualityBytes;

        for (int step = 0; step < LossyQualitySearchSteps && low <= high; step++)
        {
            ct.ThrowIfCancellationRequested();
            int quality = low + ((high - low) / 2);
            byte[]? encodedBytes = EncodeWithLossCandidate(
                bitmap,
                format,
                quality,
                ct);

            if (encodedBytes is null)
            {
                return null;
            }

            if (encodedBytes.LongLength <= maxBytes)
            {
                bestBytes = encodedBytes;
                low = quality + 1;
            }
            else
            {
                high = quality - 1;
            }
        }

        return new LossyEncodingResult(bestBytes, minimumQualityBytes);
    }

    private byte[]? EncodeWithLossCandidate(
        SKBitmap bitmap,
        AttachedImageEncodingFormat format,
        int quality,
        CancellationToken ct)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Lossy attachment encoding started. Format: {Format}, Quality: {Quality}, Width: {Width}, Height: {Height}",
            format,
            quality,
            bitmap.Width,
            bitmap.Height);
        byte[]? content = _codec.EncodeWithLoss(
            bitmap,
            format,
            quality,
            ct);
        _logger.LogInformation(
            "Lossy attachment encoding completed. Format: {Format}, Quality: {Quality}, OutputBytes: {OutputBytes}, ElapsedMilliseconds: {ElapsedMilliseconds}",
            format,
            quality,
            content?.LongLength,
            stopwatch.ElapsedMilliseconds);

        return content;
    }

    private AttachedImageDto? TryResizeToFit(
        string fileName,
        SKBitmap sourceBitmap,
        ImageModelOption selectedModel,
        AttachedImageEncodingFormat format,
        byte[]? initialMinimumBytes,
        CancellationToken ct)
    {
        SKBitmap currentBitmap = sourceBitmap;
        SKBitmap? ownedBitmap = null;
        byte[]? minimumBytes = initialMinimumBytes;

        try
        {
            for (int attempt = 0; attempt < MaximumResizeAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                if (minimumBytes is not null
                    && minimumBytes.LongLength > selectedModel.MaxAttachedImageBytes)
                {
                    SKSizeI nextSize = AttachedImagePreparationPlanner.CalculateReducedSize(
                        currentBitmap.Width,
                        currentBitmap.Height,
                        minimumBytes.LongLength,
                        selectedModel.MaxAttachedImageBytes);

                    if (nextSize.Width == currentBitmap.Width
                        && nextSize.Height == currentBitmap.Height)
                    {
                        return null;
                    }

                    SKBitmap resizedBitmap = _codec.Resize(currentBitmap, nextSize);
                    ownedBitmap?.Dispose();
                    ownedBitmap = resizedBitmap;
                    currentBitmap = resizedBitmap;
                    minimumBytes = null;
                }

                if (format == AttachedImageEncodingFormat.Png)
                {
                    byte[]? pngBytes = EncodePngFast(currentBitmap, ct);

                    if (pngBytes is null)
                    {
                        return null;
                    }

                    AttachedImageDto? preparedImage = CreatePreparedPngIfFits(
                        fileName,
                        pngBytes,
                        selectedModel.MaxAttachedImageBytes);

                    if (preparedImage is not null)
                    {
                        return preparedImage;
                    }

                    minimumBytes = pngBytes;
                    continue;
                }

                LossyEncodingResult? result = FindHighestQualityEncoding(
                    currentBitmap,
                    format,
                    selectedModel.MaxAttachedImageBytes,
                    ct);

                if (result is null)
                {
                    return null;
                }

                if (result.BestBytes is not null)
                {
                    return CreatePreparedImage(fileName, format, result.BestBytes);
                }

                minimumBytes = result.MinimumQualityBytes;
            }

            return null;
        }
        finally
        {
            ownedBitmap?.Dispose();
        }
    }

    private static bool IsCloseToLimit(long candidateBytes, long maxBytes)
    {
        return candidateBytes <= maxBytes * MaximumLosslessCandidateRatio;
    }

    private static SKSizeI CalculateEstimatedWorkingSize(
        SKSizeI formatWorkingSize,
        long estimatedBytes,
        long maxBytes)
    {
        if (estimatedBytes <= maxBytes)
        {
            return formatWorkingSize;
        }

        return AttachedImagePreparationPlanner.CalculateReducedSize(
            formatWorkingSize.Width,
            formatWorkingSize.Height,
            estimatedBytes,
            maxBytes);
    }

    private static AttachedImageEncodingFormat? ResolveLosslessFormat(ImageModelOption selectedModel)
    {
        if (Supports(selectedModel, GenerationImageContentTypes.Webp))
        {
            return AttachedImageEncodingFormat.Webp;
        }

        if (Supports(selectedModel, GenerationImageContentTypes.Png))
        {
            return AttachedImageEncodingFormat.Png;
        }

        return null;
    }

    private static AttachedImageEncodingFormat? ResolveLossyFormat(
        ImageModelOption selectedModel,
        SKAlphaType alphaType)
    {
        if (Supports(selectedModel, GenerationImageContentTypes.Webp))
        {
            return AttachedImageEncodingFormat.Webp;
        }

        if (alphaType == SKAlphaType.Opaque
            && Supports(selectedModel, GenerationImageContentTypes.Jpeg))
        {
            return AttachedImageEncodingFormat.Jpeg;
        }

        return null;
    }

    private static bool Supports(ImageModelOption selectedModel, string contentType)
    {
        return selectedModel.SupportedAttachmentContentTypes.Contains(
            contentType,
            StringComparer.OrdinalIgnoreCase);
    }

    private static AttachedImageDto? CreatePreparedPngIfFits(
        string fileName,
        byte[] content,
        long maxAttachedImageBytes)
    {
        return content.LongLength <= maxAttachedImageBytes
            ? CreatePreparedImage(fileName, AttachedImageEncodingFormat.Png, content)
            : null;
    }

    private static AttachedImageDto CreatePreparedImage(
        string fileName,
        AttachedImageEncodingFormat format,
        byte[] content)
    {
        string contentType = format switch
        {
            AttachedImageEncodingFormat.Webp => GenerationImageContentTypes.Webp,
            AttachedImageEncodingFormat.Jpeg => GenerationImageContentTypes.Jpeg,
            AttachedImageEncodingFormat.Png => GenerationImageContentTypes.Png,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

        return new AttachedImageDto(
            CreatePreparedFileName(fileName, GetPreferredExtension(contentType)),
            contentType,
            content);
    }

    private static string CreatePreparedFileName(string fileName, string extension)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "image";
        }

        return $"{baseName}{PreparedFileNameSuffix}{extension}";
    }

    private static string GetPreferredExtension(string contentType)
    {
        GenerationImageFileFormatDescriptor descriptor = GenerationImageFileFormats.All
            .Single(format => string.Equals(
                format.ContentType,
                contentType,
                StringComparison.OrdinalIgnoreCase));

        return descriptor.Extensions[0];
    }

    private bool IsAlreadyAccepted(AttachedImageDto image, ImageModelOption selectedModel)
    {
        return image.Content is { Length: > 0 }
            && image.Content.LongLength <= selectedModel.MaxAttachedImageBytes
            && _formatRegistry.TryGetByContentType(
                image.ContentType,
                out IGenerationImageFormat? format)
            && format is not null
            && selectedModel.SupportedAttachmentContentTypes.Contains(
                image.ContentType,
                StringComparer.OrdinalIgnoreCase)
            && _signatureValidator.MatchesSignature(image.ContentType, image.Content);
    }

    private sealed record EncodedImageCandidate(
        AttachedImageEncodingFormat Format,
        byte[] Content);

    private sealed record LosslessEncodingResult(
        AttachedImageDto? AcceptedImage,
        EncodedImageCandidate Candidate);

    private sealed record LossyEncodingResult(
        byte[]? BestBytes,
        byte[] MinimumQualityBytes);
}
