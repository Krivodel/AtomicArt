using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using AtomicArt.Domain.Generation;

namespace AtomicArt.Domain;

public static class DependencyInjection
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Assembly assembly = typeof(DependencyInjection).Assembly;
        IEnumerable<Type> modelRulesTypes = assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => typeof(IGenerationModelRules).IsAssignableFrom(type));

        foreach (Type modelRulesType in modelRulesTypes)
        {
            services.AddSingleton(typeof(IGenerationModelRules), modelRulesType);
        }

        services.AddSingleton<GenerationModelRules>();

        return services;
    }
}
