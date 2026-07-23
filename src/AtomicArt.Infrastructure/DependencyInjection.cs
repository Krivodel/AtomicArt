using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using AtomicArt.Application.Common.Interfaces;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Infrastructure.Generation;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;

namespace AtomicArt.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string? testGenerationImagesBaseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddGoogleInteractionsServices(configuration);
        services.AddInfrastructureGenerationServices(configuration, testGenerationImagesBaseDirectory);

        return services;
    }

    private static IServiceCollection AddInfrastructureGenerationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string? testGenerationImagesBaseDirectory)
    {
        services
            .AddOptions<TestGenerationOptions>()
            .Bind(configuration.GetSection(TestGenerationOptions.SectionName))
            .PostConfigure(options => ResolveTestGenerationImagesDirectory(options, testGenerationImagesBaseDirectory))
            .Validate(
                TestGenerationOptions.IsValid,
                "TestGeneration configuration must include a positive MaxImageBytes value.");

        services.AddSingleton<FileSystemPlaceholderImageProvider>();
        services.AddSingleton<IStreamingPlaceholderImageProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<FileSystemPlaceholderImageProvider>());
        services.AddSingleton<GoogleInteractionsResponseParser>();
        services.AddSingleton<GoogleInteractionsFailureClassifier>();
        services.AddScoped<IProviderStreamingImageGenerationProvider, GoogleStreamingImageGenerationProvider>();
        services.AddScoped<IProviderStreamingImageGenerationProvider, FakeStreamingImageGenerationProvider>();
        services.AddScoped<IStreamingImageGenerationProvider, RoutingStreamingImageGenerationProvider>();
        services.AddSingleton<IGenerationModelCatalogJsonSource, FileGenerationModelCatalogJsonSource>();

        return services;
    }

    private static void ResolveTestGenerationImagesDirectory(
        TestGenerationOptions options,
        string? testGenerationImagesBaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(options.ImagesDirectory)
            || Path.IsPathFullyQualified(options.ImagesDirectory))
        {
            return;
        }

        string baseDirectory = string.IsNullOrWhiteSpace(testGenerationImagesBaseDirectory)
            ? AppContext.BaseDirectory
            : testGenerationImagesBaseDirectory;

        options.ImagesDirectory = Path.GetFullPath(options.ImagesDirectory, baseDirectory);
    }

    private static IServiceCollection AddGoogleInteractionsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<GoogleInteractionsOptions>()
            .Bind(configuration.GetSection(GoogleInteractionsOptions.SectionName))
            .Validate(
                GoogleInteractionsOptions.IsValid,
                "GoogleInteractions configuration must include valid BaseUrl and positive TimeoutSeconds.")
            .ValidateOnStart();

        services.AddHttpClient<IGoogleInteractionsClient, GoogleInteractionsClient>((serviceProvider, httpClient) =>
        {
            GoogleInteractionsOptions options = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<GoogleInteractionsOptions>>()
                .Value;

            httpClient.BaseAddress = new Uri(options.BaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }
}
