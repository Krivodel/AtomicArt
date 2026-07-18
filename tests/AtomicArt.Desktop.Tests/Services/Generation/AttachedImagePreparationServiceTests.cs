using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class AttachedImagePreparationServiceTests
{
    private const int SourceWidth = 192;
    private const int SourceHeight = 144;

    [Fact]
    public async Task PrepareAsync_WithAcceptedImage_PreservesOriginalBytes()
    {
        byte[] content = CreateDeterministicPng();
        AttachedImageDto image = new(
            "source.png",
            GenerationImageContentTypes.Png,
            content);
        ImageModelOption selectedModel = CreateModel(content.Length + 1);
        AttachedImagePreparationService service = CreateService();

        AttachedImageDto? result = await service.PrepareAsync(
            image,
            selectedModel,
            CancellationToken.None);

        result.Should().BeSameAs(image);
        result?.Content.Should().BeSameAs(content);
    }

    [Fact]
    public async Task PrepareAsync_WhenFastLosslessEncodingFits_PreservesPixels()
    {
        byte[] encodedPng = CreateDeterministicPng();
        SkiaAttachedImageCodec codec = new();
        using SKBitmap bitmap = Decode(encodedPng);
        byte[] fastLosslessBytes = codec.EncodeLosslessly(
                bitmap,
                AttachedImageEncodingFormat.Webp,
                AttachedImageCompressionEffort.Fast,
                CancellationToken.None)
            ?? throw new InvalidOperationException("Test WebP could not be encoded.");
        byte[] content = new byte[Math.Max(encodedPng.Length, fastLosslessBytes.Length) + 1];
        encodedPng.CopyTo(content, 0);
        AttachedImageDto image = new(
            "source.png",
            GenerationImageContentTypes.Png,
            content);
        ImageModelOption selectedModel = CreateModel(fastLosslessBytes.Length);
        AttachedImagePreparationService service = CreateService();

        AttachedImageDto? result = await service.PrepareAsync(
            image,
            selectedModel,
            CancellationToken.None);

        result.Should().NotBeNull();
        result?.Content.LongLength.Should().BeLessThanOrEqualTo(selectedModel.MaxAttachedImageBytes);
        using SKBitmap sourceBitmap = Decode(content);
        using SKBitmap preparedBitmap = Decode(result?.Content ?? []);
        preparedBitmap.Width.Should().Be(sourceBitmap.Width);
        preparedBitmap.Height.Should().Be(sourceBitmap.Height);
        ReadPixels(preparedBitmap).Should().Equal(ReadPixels(sourceBitmap));
    }

    [Fact]
    public async Task PrepareAsync_WithStrictLimit_ResizesUntilImageFits()
    {
        byte[] content = CreateDeterministicPng();
        AttachedImageDto image = new(
            "source.png",
            GenerationImageContentTypes.Png,
            content);
        ImageModelOption selectedModel = CreateModel(1500);
        AttachedImagePreparationService service = CreateService();

        AttachedImageDto? result = await service.PrepareAsync(
            image,
            selectedModel,
            CancellationToken.None);

        result.Should().NotBeNull();
        result?.Content.LongLength.Should().BeLessThanOrEqualTo(selectedModel.MaxAttachedImageBytes);
        using SKBitmap preparedBitmap = Decode(result?.Content ?? []);
        preparedBitmap.Width.Should().BeLessThan(SourceWidth);
        preparedBitmap.Height.Should().BeLessThan(SourceHeight);
    }

    [Fact]
    public async Task PrepareAsync_WithUnstorableContentType_ConvertsToManagedFormat()
    {
        byte[] content = CreateDeterministicPng();
        AttachedImageDto image = new(
            "source.heic",
            GenerationImageContentTypes.Heic,
            content);
        ImageModelOption selectedModel = CreateModel(content.Length * 2);
        AttachedImagePreparationService service = CreateService();

        AttachedImageDto? result = await service.PrepareAsync(
            image,
            selectedModel,
            CancellationToken.None);

        result.Should().NotBeNull();
        result?.ContentType.Should().BeOneOf(
            GenerationImageContentTypes.Png,
            GenerationImageContentTypes.Webp);
        result?.FileName.Should().NotEndWith(".heic");
    }

    [Fact]
    public async Task PrepareAsync_WhenFastLosslessIsFarOverLimitAndQualityHundredFits_EncodesTwice()
    {
        RecordingAttachedImageCodec codec = new()
        {
            LosslessEncoder = _ => new byte[200],
            LossyEncoder = quality => quality == 100
                ? new byte[90]
                : throw new InvalidOperationException("Unexpected lossy quality.")
        };
        AttachedImageDto image = new(
            "source.png",
            GenerationImageContentTypes.Png,
            new byte[101]);
        ImageModelOption selectedModel = CreateModel(100);
        AttachedImagePreparationService service = CreateService(codec);

        AttachedImageDto? result = await service.PrepareAsync(
            image,
            selectedModel,
            CancellationToken.None);

        result.Should().NotBeNull();
        codec.LosslessCalls.Should().Equal(
            (AttachedImageEncodingFormat.Webp, AttachedImageCompressionEffort.Fast));
        codec.LossyCalls.Should().Equal(
            (AttachedImageEncodingFormat.Webp, 100));
    }

    [Fact]
    public async Task PrepareAsync_WhenFastLosslessIsCloseToLimit_TriesMaximumLossless()
    {
        RecordingAttachedImageCodec codec = new()
        {
            LosslessEncoder = effort => effort == AttachedImageCompressionEffort.Fast
                ? new byte[104]
                : new byte[100],
            LossyEncoder = _ => throw new InvalidOperationException("Lossy encoding was not expected.")
        };
        AttachedImageDto image = new(
            "source.png",
            GenerationImageContentTypes.Png,
            new byte[101]);
        ImageModelOption selectedModel = CreateModel(100);
        AttachedImagePreparationService service = CreateService(codec);

        AttachedImageDto? result = await service.PrepareAsync(
            image,
            selectedModel,
            CancellationToken.None);

        result.Should().NotBeNull();
        codec.LosslessCalls.Should().Equal(
            (AttachedImageEncodingFormat.Webp, AttachedImageCompressionEffort.Fast),
            (AttachedImageEncodingFormat.Webp, AttachedImageCompressionEffort.Maximum));
        codec.LossyCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenMinimumQualityDoesNotFit_ReusesItForResize()
    {
        int maximumQualityAttempts = 0;
        RecordingAttachedImageCodec codec = new()
        {
            LosslessEncoder = _ => new byte[200],
            LossyEncoder = quality =>
            {
                if (quality == 35)
                {
                    return new byte[150];
                }

                if (quality == 100)
                {
                    maximumQualityAttempts++;

                    return maximumQualityAttempts == 1
                        ? new byte[200]
                        : new byte[90];
                }

                throw new InvalidOperationException("Unexpected lossy quality.");
            }
        };
        AttachedImageDto image = new(
            "source.png",
            GenerationImageContentTypes.Png,
            new byte[101]);
        ImageModelOption selectedModel = CreateModel(100);
        AttachedImagePreparationService service = CreateService(codec);

        AttachedImageDto? result = await service.PrepareAsync(
            image,
            selectedModel,
            CancellationToken.None);

        result.Should().NotBeNull();
        codec.LossyCalls.Select(call => call.Quality)
            .Should()
            .Equal(100, 35, 100);
        codec.ResizeCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task PrepareAsync_WhenQualitySearchIsNeeded_EncodesKnownBoundsOnce()
    {
        RecordingAttachedImageCodec codec = new()
        {
            LosslessEncoder = _ => new byte[200],
            LossyEncoder = quality => quality <= 75
                ? new byte[90]
                : new byte[110]
        };
        AttachedImageDto image = new(
            "source.png",
            GenerationImageContentTypes.Png,
            new byte[101]);
        ImageModelOption selectedModel = CreateModel(100);
        AttachedImagePreparationService service = CreateService(codec);

        AttachedImageDto? result = await service.PrepareAsync(
            image,
            selectedModel,
            CancellationToken.None);

        result.Should().NotBeNull();
        codec.LossyCalls.Count(call => call.Quality == 100).Should().Be(1);
        codec.LossyCalls.Count(call => call.Quality == 35).Should().Be(1);
        codec.LossyCalls.Should().HaveCountLessThanOrEqualTo(8);
    }

    [Fact]
    public async Task PrepareAsync_WhenLargeProbePredictsOversizedEncoding_ResizesBeforeFullEncoding()
    {
        const int largeWidth = 10923;
        const int largeHeight = AttachedImagePreparationPlanner.MaximumWebpDimension;
        RecordingAttachedImageCodec codec = new()
        {
            ImageInfo = new AttachedImageCodecInfo(
                largeWidth,
                largeHeight,
                SKAlphaType.Opaque),
            LosslessEncoder = _ => new byte[200],
            LossyEncoder = quality => quality == 100
                ? new byte[90]
                : throw new InvalidOperationException("Unexpected lossy quality.")
        };
        AttachedImageDto image = new(
            "source.png",
            GenerationImageContentTypes.Png,
            new byte[101]);
        ImageModelOption selectedModel = CreateModel(100);
        AttachedImagePreparationService service = CreateService(codec);

        AttachedImageDto? result = await service.PrepareAsync(
            image,
            selectedModel,
            CancellationToken.None);

        result.Should().NotBeNull();
        codec.DecodeCallCount.Should().Be(1);
        codec.LosslessCalls.Should().Equal(
            (AttachedImageEncodingFormat.Webp, AttachedImageCompressionEffort.Fast));
        codec.LossyCalls.Should().Equal(
            (AttachedImageEncodingFormat.Webp, 100),
            (AttachedImageEncodingFormat.Webp, 100));
        codec.ResizeCalls.Should().HaveCount(2);
        codec.ResizeCalls.Last().Width.Should().BeLessThan(largeWidth);
        codec.ResizeCalls.Last().Height.Should().BeLessThan(largeHeight);
    }

    [Fact]
    public async Task PrepareAsync_WhenLargeLosslessProbeFits_DoesNotReduceWorkingResolution()
    {
        const int largeWidth = 10923;
        const int largeHeight = AttachedImagePreparationPlanner.MaximumWebpDimension;
        RecordingAttachedImageCodec codec = new()
        {
            ImageInfo = new AttachedImageCodecInfo(
                largeWidth,
                largeHeight,
                SKAlphaType.Opaque),
            LosslessEncoder = _ => new byte[100],
            LossyEncoder = _ => throw new InvalidOperationException(
                "Lossy encoding was not expected.")
        };
        AttachedImageDto image = new(
            "source.png",
            GenerationImageContentTypes.Png,
            new byte[10001]);
        ImageModelOption selectedModel = CreateModel(10000);
        AttachedImagePreparationService service = CreateService(codec);

        AttachedImageDto? result = await service.PrepareAsync(
            image,
            selectedModel,
            CancellationToken.None);

        result.Should().NotBeNull();
        codec.DecodeCallCount.Should().Be(1);
        codec.ResizeCalls.Should().ContainSingle();
        codec.ResizeCalls.Single().Should().Be(
            AttachedImagePreparationPlanner.CalculateEncodingProbeSize(codec.ImageInfo));
        codec.LosslessCalls.Should().Equal(
            (AttachedImageEncodingFormat.Webp, AttachedImageCompressionEffort.Fast),
            (AttachedImageEncodingFormat.Webp, AttachedImageCompressionEffort.Fast));
        codec.LossyCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenWebpDimensionIsTooLarge_ResizesBeforeEncoding()
    {
        const int largeWidth = 20000;
        const int sourceHeight = 1000;
        RecordingAttachedImageCodec codec = new()
        {
            ImageInfo = new AttachedImageCodecInfo(
                largeWidth,
                sourceHeight,
                SKAlphaType.Opaque),
            LosslessEncoder = _ => throw new InvalidOperationException(
                "Oversized WebP lossless encoding was not expected."),
            LossyEncoder = quality => quality == 100
                ? new byte[90]
                : throw new InvalidOperationException("Unexpected lossy quality.")
        };
        AttachedImageDto image = new(
            "source.png",
            GenerationImageContentTypes.Png,
            new byte[101]);
        ImageModelOption selectedModel = CreateModel(100);
        AttachedImagePreparationService service = CreateService(codec);

        AttachedImageDto? result = await service.PrepareAsync(
            image,
            selectedModel,
            CancellationToken.None);

        result.Should().NotBeNull();
        codec.DecodeCallCount.Should().Be(1);
        codec.ResizeCalls.Should().ContainSingle();
        codec.ResizeCalls.Single().Width
            .Should()
            .BeLessThanOrEqualTo(AttachedImagePreparationPlanner.MaximumWebpDimension);
        codec.LosslessCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenLargeImageSupportsOnlyPng_EncodesEachResolutionOnce()
    {
        RecordingAttachedImageCodec codec = new()
        {
            ImageInfo = new AttachedImageCodecInfo(
                10923,
                16384,
                SKAlphaType.Opaque),
            LosslessEncoder = _ => new byte[90],
            LossyEncoder = _ => throw new InvalidOperationException(
                "Lossy encoding was not expected.")
        };
        AttachedImageDto image = new(
            "source.png",
            GenerationImageContentTypes.Png,
            new byte[101]);
        ImageModelOption selectedModel = CreateModel(
            100,
            [GenerationImageContentTypes.Png]);
        AttachedImagePreparationService service = CreateService(codec);

        AttachedImageDto? result = await service.PrepareAsync(
            image,
            selectedModel,
            CancellationToken.None);

        result.Should().NotBeNull();
        codec.DecodeCallCount.Should().Be(1);
        codec.ResizeCalls.Should().HaveCount(2);
        codec.LosslessCalls.Should().Equal(
            (AttachedImageEncodingFormat.Png, AttachedImageCompressionEffort.Fast),
            (AttachedImageEncodingFormat.Png, AttachedImageCompressionEffort.Fast));
        codec.LossyCalls.Should().BeEmpty();
    }

    [Fact]
    public void ReadInfo_WithPng_ReturnsDimensionsWithoutPixelDecode()
    {
        byte[] content = CreateDeterministicPng();
        SkiaAttachedImageCodec codec = new();

        AttachedImageCodecInfo? result = codec.ReadInfo(content);

        result.Should().NotBeNull();
        result?.Width.Should().Be(SourceWidth);
        result?.Height.Should().Be(SourceHeight);
    }

    private static AttachedImagePreparationService CreateService(
        IAttachedImageCodec? codec = null)
    {
        return new AttachedImagePreparationService(
            new AttachedImageSignatureValidator(),
            GenerationImageFormatRegistryTestFactory.Create(),
            codec ?? new SkiaAttachedImageCodec(),
            new AttachedImagePreparationConcurrencyLimiter(),
            NullLogger<AttachedImagePreparationService>.Instance);
    }

    private static ImageModelOption CreateModel(
        int maxAttachedImageBytes,
        IReadOnlyList<string>? supportedContentTypes = null)
    {
        return new ImageModelOption(
            "model",
            "Model",
            "provider",
            "provider-model",
            "panel",
            1024,
            1024,
            ["1:1"],
            ["1K"],
            [1],
            new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d),
            8,
            maxAttachedImageBytes,
            32L * 1024L * 1024L,
            supportedContentTypes
            ??
            [
                GenerationImageContentTypes.Png,
                GenerationImageContentTypes.Jpeg,
                GenerationImageContentTypes.Webp
            ],
            GenerationModelPricingMetadataTestFactory.CreateFreePricing());
    }

    private static byte[] CreateDeterministicPng()
    {
        using SKBitmap bitmap = new(
            SourceWidth,
            SourceHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        for (int y = 0; y < SourceHeight; y++)
        {
            for (int x = 0; x < SourceWidth; x++)
            {
                bitmap.SetPixel(
                    x,
                    y,
                    new SKColor(
                        (byte)((x * 37 + y * 11) % byte.MaxValue),
                        (byte)((x * 13 + y * 29) % byte.MaxValue),
                        (byte)((x * 7 + y * 43) % byte.MaxValue)));
            }
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Test PNG could not be encoded.");

        return data.ToArray();
    }

    private static SKBitmap Decode(byte[] content)
    {
        return SKBitmap.Decode(content)
            ?? throw new InvalidOperationException("Test image could not be decoded.");
    }

    private static SKColor[] ReadPixels(SKBitmap bitmap)
    {
        SKColor[] pixels = new SKColor[bitmap.Width * bitmap.Height];

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                pixels[(y * bitmap.Width) + x] = bitmap.GetPixel(x, y);
            }
        }

        return pixels;
    }
}
