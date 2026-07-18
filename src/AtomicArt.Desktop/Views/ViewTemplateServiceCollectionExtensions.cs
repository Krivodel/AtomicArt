using Microsoft.Extensions.DependencyInjection;

using Avalonia.Controls;

namespace AtomicArt.Desktop.Views;

internal static class ViewTemplateServiceCollectionExtensions
{
    internal static IServiceCollection AddViewTemplate<TViewModel, TView>(
        this IServiceCollection services)
        where TView : Control
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<TView>();
        services.AddSingleton(provider => new ViewTemplateRegistration(
            typeof(TViewModel),
            provider.GetRequiredService<TView>));

        return services;
    }
}
