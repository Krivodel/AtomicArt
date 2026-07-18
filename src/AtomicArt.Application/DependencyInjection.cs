using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using FluentValidation;
using MediatR;

using AtomicArt.Application.Common.Behaviors;
using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Assembly applicationAssembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(applicationAssembly);
            configuration.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            configuration.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddImageModelDefinitionFactoriesByConvention(applicationAssembly);
        services.AddAttachedImageFormats();
        services.AddSingleton<IAttachedImageFormatRegistry, AttachedImageFormatRegistry>();
        services.AddSingleton<IImageModelRegistry, ImageModelRegistry>();
        services.AddSingleton<GenerationUsagePriceCalculator>();

        return services;
    }

    private static IServiceCollection AddAttachedImageFormats(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (GenerationImageFileFormatDescriptor descriptor in GenerationImageFileFormats.All)
        {
            services.AddSingleton<IAttachedImageFormat>(new AttachedImageFormat(descriptor));
        }

        return services;
    }

    private static IServiceCollection AddImageModelDefinitionFactoriesByConvention(
        this IServiceCollection services,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        IEnumerable<Type> factoryTypes = assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => typeof(IImageModelDefinitionFactory).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (Type factoryType in factoryTypes)
        {
            services.AddSingleton(typeof(IImageModelDefinitionFactory), factoryType);
        }

        return services;
    }
}
