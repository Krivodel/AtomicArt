using Microsoft.Extensions.DependencyInjection;

namespace AtomicArt.Desktop.Services;

internal static class ServiceCollectionImplementationRegistrationExtensions
{
    internal static IServiceCollection AddSharedSingletonImplementation(
        this IServiceCollection services,
        Type serviceType,
        Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);

        services.AddSingleton(implementationType);
        services.AddSingleton(
            serviceType,
            provider => provider.GetRequiredService(implementationType));

        return services;
    }

    internal static IServiceCollection AddSingletonImplementations(
        this IServiceCollection services,
        Type serviceType,
        IReadOnlyCollection<Type> implementationTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationTypes);

        foreach (Type implementationType in implementationTypes)
        {
            ArgumentNullException.ThrowIfNull(implementationType);
            services.AddSingleton(serviceType, implementationType);
        }

        return services;
    }
}
