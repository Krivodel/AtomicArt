using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using AtomicArt.Application.Common.Interfaces;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Infrastructure.Generation;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;
using AtomicArt.Infrastructure.Generation.OpenRouter;

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
        services.AddOpenRouterImageServices(configuration);
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
                "TestGeneration configuration must include valid provider and model settings.")
            .ValidateOnStart();

        services.AddSingleton<FileSystemPlaceholderImageProvider>();
        services.AddSingleton<IStreamingPlaceholderImageProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<FileSystemPlaceholderImageProvider>());
        services.AddSingleton<GoogleInteractionsResponseParser>();
        services.AddSingleton<GoogleInteractionsFailureClassifier>();
        services.AddSingleton<OpenRouterImageFailureClassifier>();
        services.AddScoped<IProviderStreamingImageGenerationProvider, GoogleStreamingImageGenerationProvider>();
        services.AddScoped<IProviderStreamingImageGenerationProvider, OpenRouterStreamingImageGenerationProvider>();
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
                "GoogleInteractions configuration must include a valid BaseUrl and positive response limits.")
            .ValidateOnStart();

        services.AddHttpClient<IGoogleInteractionsClient, GoogleInteractionsClient>((serviceProvider, httpClient) =>
        {
            GoogleInteractionsOptions options = serviceProvider
                .GetRequiredService<IOptions<GoogleInteractionsOptions>>()
                .Value;

            httpClient.BaseAddress = new Uri(options.BaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(
                options.ProviderResponseTimeoutSeconds);
        });

        return services;
    }

    private static IServiceCollection AddOpenRouterImageServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<OpenRouterImageOptions>()
            .Bind(configuration.GetSection(OpenRouterImageOptions.SectionName))
            .Validate(
                OpenRouterImageOptions.IsValid,
                "OpenRouter Image configuration must include a valid BaseUrl and positive response limits.")
            .ValidateOnStart();

        services.AddHttpClient<IOpenRouterImageClient, OpenRouterImageClient>((serviceProvider, httpClient) =>
        {
            OpenRouterImageOptions options = serviceProvider
                .GetRequiredService<IOptions<OpenRouterImageOptions>>()
                .Value;
            httpClient.BaseAddress = new Uri(options.BaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(options.ProviderResponseTimeoutSeconds);
        });

        return services;
    }
}
