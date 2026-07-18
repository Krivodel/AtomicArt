using Microsoft.Extensions.DependencyInjection;

using Avalonia.Controls;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Views;

namespace AtomicArt.Desktop.Tests.Views;

public sealed class ViewTemplateServiceCollectionExtensionsTests
{
    [Fact]
    public void AddViewTemplate_WithMapping_RegistersTransientViewFactory()
    {
        ServiceCollection services = new();
        services.AddViewTemplate<object, Control>();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        ViewTemplateRegistration registration = serviceProvider
            .GetRequiredService<ViewTemplateRegistration>();

        Control firstView = registration.CreateView();
        Control secondView = registration.CreateView();

        registration.ViewModelType.Should().Be(typeof(object));
        firstView.Should().BeOfType<Control>();
        secondView.Should().BeOfType<Control>();
        secondView.Should().NotBeSameAs(firstView);
        services.Single(descriptor => descriptor.ServiceType == typeof(Control))
            .Lifetime.Should().Be(ServiceLifetime.Transient);
    }
}
