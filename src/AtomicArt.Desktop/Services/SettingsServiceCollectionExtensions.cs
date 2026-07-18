using Microsoft.Extensions.DependencyInjection;

using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Services;

public static class SettingsServiceCollectionExtensions
{
    public static IServiceCollection AddSettingsDefinitionsByConvention(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Type settingsDefinitionType = typeof(ISettingsDefinition);
        Type scaleOptionDefinitionType = typeof(IUiScaleOptionDefinition);
        IEnumerable<Type> definitionTypes = typeof(SettingsServiceCollectionExtensions)
            .Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsPublic: true })
            .Where(type => settingsDefinitionType.IsAssignableFrom(type)
                || scaleOptionDefinitionType.IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (Type definitionType in definitionTypes)
        {
            AddDefinition(services, definitionType, settingsDefinitionType, scaleOptionDefinitionType);
        }

        return services;
    }

    public static IServiceCollection AddSettingsItemViewModelFactoriesByConvention(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Type factoryType = typeof(ISettingsItemViewModelFactory);
        IEnumerable<Type> factoryTypes = typeof(SettingsServiceCollectionExtensions)
            .Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsPublic: true })
            .Where(type => factoryType.IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (Type currentFactoryType in factoryTypes)
        {
            services.AddSingleton(factoryType, currentFactoryType);
        }

        return services;
    }

    public static IServiceCollection AddSettingsStateApplicatorsByConvention(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Type applicatorType = typeof(ISettingsStateApplicator);
        IEnumerable<Type> applicatorTypes = typeof(SettingsServiceCollectionExtensions)
            .Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsPublic: true })
            .Where(type => applicatorType.IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (Type currentApplicatorType in applicatorTypes)
        {
            services.AddSingleton(applicatorType, currentApplicatorType);
        }

        return services;
    }

    private static void AddDefinition(
        IServiceCollection services,
        Type definitionType,
        Type settingsDefinitionType,
        Type scaleOptionDefinitionType)
    {
        if (settingsDefinitionType.IsAssignableFrom(definitionType))
        {
            services.AddSingleton(settingsDefinitionType, definitionType);
        }

        if (scaleOptionDefinitionType.IsAssignableFrom(definitionType))
        {
            services.AddSingleton(scaleOptionDefinitionType, definitionType);
        }
    }
}
