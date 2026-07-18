using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Infrastructure.Generation;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructureServices_WithServices_RegistersGenerationPorts()
    {
        ServiceCollection services = [];
        IConfiguration configuration = CreateConfiguration();

        services.AddInfrastructureServices(configuration);

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IImageGenerationOutputPlanner));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IImageGenerationContentProvider));
    }

    [Fact]
    public void AddInfrastructureServices_WithServices_ResolvesGenerationContentProvider()
    {
        IConfiguration configuration = CreateConfiguration();
        using ServiceProvider serviceProvider = CreateServiceProvider(
            configuration,
            services => services.AddSingleton<GenerationUsagePriceCalculator>());

        IImageGenerationContentProvider provider = serviceProvider
            .GetRequiredService<IImageGenerationContentProvider>();

        provider.GetType().FullName.Should().Be(
            "AtomicArt.Infrastructure.Generation.RoutingImageGenerationContentProvider");
    }

    [Fact]
    public void AddInfrastructureServices_WithServices_RegistersGoogleInteractionsOptions()
    {
        IConfiguration configuration = CreateConfiguration();

        using ServiceProvider serviceProvider = CreateServiceProvider(configuration);

        GoogleInteractionsOptions options = GetOptions<GoogleInteractionsOptions>(serviceProvider);

        options.BaseUrl.Should().Be(GoogleInteractionsOptions.DefaultBaseUrl);
    }

    [Fact]
    public void AddInfrastructureServices_WithServices_RegistersTestGenerationOptions()
    {
        IConfiguration configuration = CreateConfiguration();

        using ServiceProvider serviceProvider = CreateServiceProvider(configuration);

        TestGenerationOptions options = GetOptions<TestGenerationOptions>(serviceProvider);

        options.Enabled.Should().BeFalse();
        options.MaxImageBytes.Should().Be(TestGenerationOptions.DefaultMaxImageBytes);
    }

    [Fact]
    public void AddInfrastructureServices_WithRelativeTestGenerationImagesDirectory_ResolvesFromBaseDirectory()
    {
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [CreateTestGenerationKey(nameof(TestGenerationOptions.ImagesDirectory))] = "TestGenerationImages"
        });
        string baseDirectory = Path.Combine(Path.GetTempPath(), "AtomicArt.Infrastructure.Tests", Guid.NewGuid().ToString("N"));

        using ServiceProvider serviceProvider = CreateServiceProvider(
            configuration,
            baseDirectory);

        TestGenerationOptions options = GetOptions<TestGenerationOptions>(serviceProvider);

        options.ImagesDirectory.Should().Be(Path.GetFullPath(
            Path.Combine(baseDirectory, "TestGenerationImages")));
    }

    [Theory]
    [InlineData("http://generativelanguage.googleapis.com")]
    [InlineData("https://example.invalid")]
    public void GoogleInteractionsOptions_WithUnsafeBaseUrl_IsInvalid(string baseUrl)
    {
        GoogleInteractionsOptions options = new()
        {
            BaseUrl = baseUrl,
            TimeoutSeconds = 30
        };

        bool isValid = GoogleInteractionsOptions.IsValid(options);

        isValid.Should().BeFalse();
    }

    private static IConfiguration CreateConfiguration(
        IDictionary<string, string?>? additionalValues = null)
    {
        Dictionary<string, string?> values = GoogleInteractionsTestConfiguration.Create();

        if (additionalValues is not null)
        {
            foreach (KeyValuePair<string, string?> value in additionalValues)
            {
                values[value.Key] = value.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static ServiceProvider CreateServiceProvider(
        IConfiguration configuration,
        Action<ServiceCollection>? configureServices = null)
    {
        return CreateServiceProvider(
            configuration,
            null,
            configureServices);
    }

    private static ServiceProvider CreateServiceProvider(
        IConfiguration configuration,
        string? testGenerationImagesBaseDirectory,
        Action<ServiceCollection>? configureServices = null)
    {
        ServiceCollection services = [];
        configureServices?.Invoke(services);
        services.AddInfrastructureServices(
            configuration,
            testGenerationImagesBaseDirectory);

        return services.BuildServiceProvider();
    }

    private static string CreateTestGenerationKey(string key)
    {
        return $"{TestGenerationOptions.SectionName}:{key}";
    }

    private static TOptions GetOptions<TOptions>(ServiceProvider serviceProvider)
        where TOptions : class
    {
        return serviceProvider
            .GetRequiredService<IOptions<TOptions>>()
            .Value;
    }
}
