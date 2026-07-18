using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Infrastructure.Generation;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;

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
        ServiceCollection services = [];
        IConfiguration configuration = CreateConfiguration();
        services.AddSingleton<GenerationUsagePriceCalculator>();

        services.AddInfrastructureServices(configuration);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        IImageGenerationContentProvider provider = serviceProvider
            .GetRequiredService<IImageGenerationContentProvider>();

        provider.GetType().FullName.Should().Be(
            "AtomicArt.Infrastructure.Generation.RoutingImageGenerationContentProvider");
    }

    [Fact]
    public void AddInfrastructureServices_WithServices_RegistersGoogleInteractionsOptions()
    {
        ServiceCollection services = [];
        IConfiguration configuration = CreateConfiguration();

        services.AddInfrastructureServices(configuration);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GoogleInteractionsOptions options = serviceProvider
            .GetRequiredService<IOptions<GoogleInteractionsOptions>>()
            .Value;

        options.BaseUrl.Should().Be(GoogleInteractionsOptions.DefaultBaseUrl);
    }

    [Fact]
    public void AddInfrastructureServices_WithServices_RegistersTestGenerationOptions()
    {
        ServiceCollection services = [];
        IConfiguration configuration = CreateConfiguration();

        services.AddInfrastructureServices(configuration);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        TestGenerationOptions options = serviceProvider
            .GetRequiredService<IOptions<TestGenerationOptions>>()
            .Value;

        options.Enabled.Should().BeFalse();
        options.MaxImageBytes.Should().Be(TestGenerationOptions.DefaultMaxImageBytes);
    }

    [Fact]
    public void AddInfrastructureServices_WithRelativeTestGenerationImagesDirectory_ResolvesFromBaseDirectory()
    {
        ServiceCollection services = [];
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [CreateTestGenerationKey(nameof(TestGenerationOptions.ImagesDirectory))] = "TestGenerationImages"
        });
        string baseDirectory = Path.Combine(Path.GetTempPath(), "AtomicArt.Infrastructure.Tests", Guid.NewGuid().ToString("N"));

        services.AddInfrastructureServices(configuration, baseDirectory);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        TestGenerationOptions options = serviceProvider
            .GetRequiredService<IOptions<TestGenerationOptions>>()
            .Value;

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
        Dictionary<string, string?> values = new(StringComparer.Ordinal)
        {
            [CreateGoogleInteractionsKey(nameof(GoogleInteractionsOptions.TimeoutSeconds))] = "30"
        };

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

    private static string CreateGoogleInteractionsKey(string key)
    {
        return $"{GoogleInteractionsOptions.SectionName}:{key}";
    }

    private static string CreateTestGenerationKey(string key)
    {
        return $"{TestGenerationOptions.SectionName}:{key}";
    }
}
