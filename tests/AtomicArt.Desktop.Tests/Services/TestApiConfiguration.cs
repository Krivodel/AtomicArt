using System.Globalization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.SingleInstance;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Services.Updates;

namespace AtomicArt.Desktop.Tests.Services;

internal static class TestApiConfiguration
{
    public const string BaseAddress = "https://atomicart.test/";
    public const int MaxAutomaticRetries = 4;
    public const int MaxConcurrentGenerations = 4;
    public const int MaxInputImageBytes = 128 * 1024 * 1024;
    public const long MaximumThumbnailSourceImageBytes = 500L * 1024L * 1024L;
    public const int ThumbnailShortSidePixels =
        GalleryThumbnailSpecification.ThumbnailShortSidePixels;

    public static IConfiguration Create(string baseAddress = BaseAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress);

        GenerationClientOptions options = CreateGenerationOptions();
        GalleryOptions galleryOptions = CreateGalleryOptions();
        ApiClientOptions apiOptions = CreateApiClientOptions();
        StatePersistenceOptions stateOptions = CreateStatePersistenceOptions();
        StorageOptions storageOptions = CreateStorageOptions();
        ApplicationUpdateOptions updateOptions = CreateApplicationUpdateOptions();
        DataTransferOptions dataTransferOptions = CreateDataTransferOptions();
        Dictionary<string, string?> values = new(StringComparer.Ordinal)
        {
            ["Api:BaseAddress"] = baseAddress,
            ["Api:ModelCatalogTimeoutSeconds"] =
                apiOptions.ModelCatalogTimeoutSeconds.ToString(
                    CultureInfo.InvariantCulture),
            ["Api:MaximumProblemDetailsErrorCodeCharacters"] =
                apiOptions.MaximumProblemDetailsErrorCodeCharacters.ToString(
                    CultureInfo.InvariantCulture),
            ["Api:MaximumProblemDetailsResponseBytes"] =
                apiOptions.MaximumProblemDetailsResponseBytes.ToString(
                    CultureInfo.InvariantCulture),
            ["Logging:File:MinimumLevel"] = "Debug",
            ["Logging:File:MaxFileSizeBytes"] = "65536",
            ["Logging:File:MaximumExceptionDepth"] = "3",
            ["Logging:File:MaximumMessageCharacters"] = "4096",
            ["Logging:File:MaximumPausedBufferBytes"] = "1048576",
            ["Logging:File:MaximumSanitizedMessageCharacters"] = "1024",
            ["Logging:File:MaximumSanitizerInputMessageCharacters"] = "4096",
            ["Logging:File:MaximumStackFrameCount"] = "16",
            ["Logging:File:RetainedFileCount"] = "2",
            ["Logging:File:RetentionDays"] = "14",
            ["Generation:AttachedImagePreparationConcurrency"] =
                options.AttachedImagePreparationConcurrency.ToString(CultureInfo.InvariantCulture),
            ["Generation:Base64DecoderInputBufferSize"] =
                options.Base64DecoderInputBufferSize.ToString(CultureInfo.InvariantCulture),
            ["Generation:Base64DecoderOutputBufferSize"] =
                options.Base64DecoderOutputBufferSize.ToString(CultureInfo.InvariantCulture),
            ["Generation:EncodingProbeActivationPixelMultiplier"] =
                options.EncodingProbeActivationPixelMultiplier.ToString(CultureInfo.InvariantCulture),
            ["Generation:EncodingProbeMaximumDimension"] =
                options.EncodingProbeMaximumDimension.ToString(CultureInfo.InvariantCulture),
            ["Generation:ExternalImageTimeoutSeconds"] =
                options.ExternalImageTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            ["Generation:FastLosslessWebpCompressionEffort"] =
                options.FastLosslessWebpCompressionEffort.ToString(CultureInfo.InvariantCulture),
            ["Generation:FastPngCompressionLevel"] =
                options.FastPngCompressionLevel.ToString(CultureInfo.InvariantCulture),
            ["Generation:LossyQualitySearchSteps"] =
                options.LossyQualitySearchSteps.ToString(CultureInfo.InvariantCulture),
            ["Generation:MaxAutomaticRetries"] =
                options.MaxAutomaticRetries.ToString(CultureInfo.InvariantCulture),
            ["Generation:MaxConcurrentGenerations"] =
                options.MaxConcurrentGenerations.ToString(CultureInfo.InvariantCulture),
            ["Generation:MaxDecodedProviderResponseImageBytes"] =
                options.MaxDecodedProviderResponseImageBytes.ToString(CultureInfo.InvariantCulture),
            ["Generation:MaxInputImageBytes"] =
                options.MaxInputImageBytes.ToString(CultureInfo.InvariantCulture),
            ["Generation:MaximumLosslessCandidateRatio"] =
                options.MaximumLosslessCandidateRatio.ToString(CultureInfo.InvariantCulture),
            ["Generation:MaximumLossyQuality"] =
                options.MaximumLossyQuality.ToString(CultureInfo.InvariantCulture),
            ["Generation:MaximumLosslessWebpCompressionEffort"] =
                options.MaximumLosslessWebpCompressionEffort.ToString(
                    CultureInfo.InvariantCulture),
            ["Generation:MaximumPngCompressionLevel"] =
                options.MaximumPngCompressionLevel.ToString(CultureInfo.InvariantCulture),
            ["Generation:MaximumResizeAttempts"] =
                options.MaximumResizeAttempts.ToString(CultureInfo.InvariantCulture),
            ["Generation:MaxResponseMetadataBytes"] =
                options.MaxResponseMetadataBytes.ToString(CultureInfo.InvariantCulture),
            ["Generation:MinimumLossyQuality"] =
                options.MinimumLossyQuality.ToString(CultureInfo.InvariantCulture),
            ["Generation:ProviderResponseTimeoutSeconds"] =
                options.ProviderResponseTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            ["Generation:ResizeSafetyFactor"] =
                options.ResizeSafetyFactor.ToString(CultureInfo.InvariantCulture),
            ["Generation:ResponseMetadataBufferSize"] =
                options.ResponseMetadataBufferSize.ToString(CultureInfo.InvariantCulture),
            ["Gallery:ElapsedRefreshIntervalMilliseconds"] =
                galleryOptions.ElapsedRefreshIntervalMilliseconds.ToString(CultureInfo.InvariantCulture),
            ["Gallery:MaximumPooledCardControlCount"] =
                galleryOptions.MaximumPooledCardControlCount.ToString(CultureInfo.InvariantCulture),
            ["Gallery:MaximumPreviewCacheSizeBytes"] =
                galleryOptions.MaximumPreviewCacheSizeBytes.ToString(CultureInfo.InvariantCulture),
            ["Gallery:MaximumPreviewDecodeConcurrency"] =
                galleryOptions.MaximumPreviewDecodeConcurrency.ToString(CultureInfo.InvariantCulture),
            ["Gallery:MaximumPreviewPresentationsPerFrame"] =
                galleryOptions.MaximumPreviewPresentationsPerFrame.ToString(CultureInfo.InvariantCulture),
            ["Gallery:MaximumThumbnailCreationConcurrency"] =
                galleryOptions.MaximumThumbnailCreationConcurrency.ToString(CultureInfo.InvariantCulture),
            ["Gallery:MaximumThumbnailSourceImageBytes"] =
                galleryOptions.MaximumThumbnailSourceImageBytes.ToString(CultureInfo.InvariantCulture),
            ["Gallery:OrderTimestampIntervalMilliseconds"] =
                galleryOptions.OrderTimestampIntervalMilliseconds.ToString(CultureInfo.InvariantCulture),
            ["Updates:CheckIntervalMinutes"] =
                updateOptions.CheckIntervalMinutes.ToString(CultureInfo.InvariantCulture),
            ["Updates:RepositoryUrl"] = updateOptions.RepositoryUrl,
            ["DataTransfer:MaximumTransferredFileNameCharacters"] =
                dataTransferOptions.MaximumTransferredFileNameCharacters.ToString(
                    CultureInfo.InvariantCulture),
            ["DataTransfer:MaximumVirtualFileCount"] =
                dataTransferOptions.MaximumVirtualFileCount.ToString(
                    CultureInfo.InvariantCulture),
            ["DataTransfer:MaximumVirtualFileDescriptorBytes"] =
                dataTransferOptions.MaximumVirtualFileDescriptorBytes.ToString(
                    CultureInfo.InvariantCulture),
            ["DataTransfer:VirtualFileStreamBufferSize"] =
                dataTransferOptions.VirtualFileStreamBufferSize.ToString(
                    CultureInfo.InvariantCulture),
            ["State:DeferredWriteDelayMilliseconds"] =
                stateOptions.DeferredWriteDelayMilliseconds.ToString(CultureInfo.InvariantCulture),
            ["Storage:DataRootFileTransferBufferSize"] =
                storageOptions.DataRootFileTransferBufferSize.ToString(CultureInfo.InvariantCulture),
            ["Storage:MaximumProtectedSecretFileBytes"] =
                storageOptions.MaximumProtectedSecretFileBytes.ToString(CultureInfo.InvariantCulture),
            ["Storage:TrustedFileStreamBufferSize"] =
                storageOptions.TrustedFileStreamBufferSize.ToString(CultureInfo.InvariantCulture)
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    public static ApiClientOptions CreateApiClientOptions()
    {
        return new ApiClientOptions
        {
            ModelCatalogTimeoutSeconds = 100,
            MaximumProblemDetailsErrorCodeCharacters = 32,
            MaximumProblemDetailsResponseBytes = 16 * 1024
        };
    }

    public static DataTransferOptions CreateDataTransferOptions()
    {
        return new DataTransferOptions
        {
            MaximumTransferredFileNameCharacters = 128,
            MaximumVirtualFileCount = 64,
            MaximumVirtualFileDescriptorBytes = 64 * 1024,
            VirtualFileStreamBufferSize = 81920
        };
    }

    public static IOptions<DataTransferOptions> CreateDataTransferOptionsWrapper()
    {
        return Options.Create(CreateDataTransferOptions());
    }

    public static IOptions<ApiClientOptions> CreateApiClientOptionsWrapper()
    {
        return Options.Create(CreateApiClientOptions());
    }

    public static GenerationClientOptions CreateGenerationOptions(
        int maxConcurrentGenerations = MaxConcurrentGenerations,
        int maxAutomaticRetries = MaxAutomaticRetries,
        int attachedImagePreparationConcurrency = 2)
    {
        return new GenerationClientOptions
        {
            AttachedImagePreparationConcurrency = attachedImagePreparationConcurrency,
            Base64DecoderInputBufferSize = 4096,
            Base64DecoderOutputBufferSize = 3072,
            EncodingProbeActivationPixelMultiplier = 8,
            EncodingProbeMaximumDimension = 2048,
            ExternalImageTimeoutSeconds = 30,
            FastLosslessWebpCompressionEffort = 35,
            FastPngCompressionLevel = 3,
            LossyQualitySearchSteps = 6,
            MaxAutomaticRetries = maxAutomaticRetries,
            MaxConcurrentGenerations = maxConcurrentGenerations,
            MaxDecodedProviderResponseImageBytes = 512L * 1024L * 1024L,
            MaxInputImageBytes = MaxInputImageBytes,
            MaximumLosslessCandidateRatio = 1.05d,
            MaximumLossyQuality = 100,
            MaximumLosslessWebpCompressionEffort = 100,
            MaximumPngCompressionLevel = 9,
            MaximumResizeAttempts = 6,
            MaxResponseMetadataBytes = 256 * 1024,
            MinimumLossyQuality = 35,
            ProviderResponseTimeoutSeconds = 900,
            ResizeSafetyFactor = 0.92d,
            ResponseMetadataBufferSize = 4096
        };
    }

    public static GalleryOptions CreateGalleryOptions()
    {
        return new GalleryOptions
        {
            ElapsedRefreshIntervalMilliseconds = 1000,
            MaximumPooledCardControlCount = 64,
            MaximumPreviewCacheSizeBytes = 64L * 1024L * 1024L,
            MaximumPreviewDecodeConcurrency = 4,
            MaximumPreviewPresentationsPerFrame = 1,
            MaximumThumbnailCreationConcurrency = 1,
            MaximumThumbnailSourceImageBytes = MaximumThumbnailSourceImageBytes,
            OrderTimestampIntervalMilliseconds = 2000
        };
    }

    public static SingleInstanceOptions CreateSingleInstanceOptions()
    {
        return new SingleInstanceOptions
        {
            ClientProtocolTimeoutMilliseconds = 5000,
            ListenerRetryDelayMilliseconds = 100,
            PipeConnectAttemptCount = 20,
            PipeConnectRetryDelayMilliseconds = 50,
            PipeConnectTimeoutMilliseconds = 150
        };
    }

    public static StatePersistenceOptions CreateStatePersistenceOptions()
    {
        return new StatePersistenceOptions
        {
            DeferredWriteDelayMilliseconds = 350
        };
    }

    public static StateWritePolicy CreateStateWritePolicy()
    {
        return new StateWritePolicy(
            Options.Create(CreateStatePersistenceOptions()));
    }

    public static StorageOptions CreateStorageOptions()
    {
        return new StorageOptions
        {
            DataRootFileTransferBufferSize = 1024 * 1024,
            MaximumProtectedSecretFileBytes = 64 * 1024,
            TrustedFileStreamBufferSize = 81920
        };
    }

    public static IOptions<StorageOptions> CreateStorageOptionsWrapper()
    {
        return Options.Create(CreateStorageOptions());
    }

    public static TrustedFileStreamFactory CreateTrustedFileStreamFactory()
    {
        return new TrustedFileStreamFactory(
            CreateStorageOptionsWrapper());
    }

    public static ApplicationUpdateOptions CreateApplicationUpdateOptions()
    {
        return new ApplicationUpdateOptions
        {
            CheckIntervalMinutes = 30,
            RepositoryUrl = "https://github.com/Krivodel/AtomicArt"
        };
    }

    public static IOptions<ApplicationUpdateOptions> CreateApplicationUpdateOptionsWrapper()
    {
        return Options.Create(CreateApplicationUpdateOptions());
    }

    public static IOptions<GalleryOptions> CreateGalleryOptionsWrapper()
    {
        return Options.Create(CreateGalleryOptions());
    }

    public static GalleryOrderTimestampNormalizer CreateGalleryOrderTimestampNormalizer()
    {
        return new GalleryOrderTimestampNormalizer(
            CreateGalleryOrderTimestampPolicy());
    }

    public static GalleryOrderTimestampPolicy CreateGalleryOrderTimestampPolicy()
    {
        return new GalleryOrderTimestampPolicy(
            CreateGalleryOptionsWrapper());
    }

    public static GalleryThumbnailGenerator CreateGalleryThumbnailGenerator()
    {
        IOptions<GalleryOptions> options = CreateGalleryOptionsWrapper();
        GalleryThumbnailSpecification specification = new(options);
        GalleryThumbnailSizeCalculator sizeCalculator = new(specification);

        return new GalleryThumbnailGenerator(
            new GalleryThumbnailImageFormat(),
            sizeCalculator,
            specification,
            options);
    }

    public static GalleryThumbnailSpecification CreateGalleryThumbnailSpecification()
    {
        return new GalleryThumbnailSpecification(
            CreateGalleryOptionsWrapper());
    }

    public static IOptions<GenerationClientOptions> CreateGenerationOptionsWrapper(
        int maxConcurrentGenerations = MaxConcurrentGenerations,
        int maxAutomaticRetries = MaxAutomaticRetries,
        int attachedImagePreparationConcurrency = 2)
    {
        GenerationClientOptions options = CreateGenerationOptions(
            maxConcurrentGenerations,
            maxAutomaticRetries,
            attachedImagePreparationConcurrency);

        return Options.Create(options);
    }
}
