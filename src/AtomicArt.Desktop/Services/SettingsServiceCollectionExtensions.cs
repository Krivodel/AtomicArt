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
        IReadOnlyList<Type> definitionTypes = DesktopTypeDiscovery.FindPublicImplementations(
            settingsDefinitionType,
            scaleOptionDefinitionType);

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
        IReadOnlyList<Type> factoryTypes =
            DesktopTypeDiscovery.FindPublicImplementations(factoryType);

        services.AddSingletonImplementations(factoryType, factoryTypes);

        return services;
    }

    public static IServiceCollection AddSettingsStateApplicatorsByConvention(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Type applicatorType = typeof(ISettingsStateApplicator);
        IReadOnlyList<Type> applicatorTypes =
            DesktopTypeDiscovery.FindPublicImplementations(applicatorType);

        services.AddSingletonImplementations(applicatorType, applicatorTypes);

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
