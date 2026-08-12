using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class DeletionConfirmationServiceTests
{
    [Fact]
    public void Constructor_WithRegisteredDefinition_UsesDefinitionDefault()
    {
        ConfirmDeletionSettingDefinition definition = new();

        DeletionConfirmationService service = CreateService(definition);

        service.IsConfirmationRequired.Should().Be(definition.DefaultValue);
    }

    [Fact]
    public void SetConfirmationRequired_WithChangedValue_UpdatesValueAndRaisesEvent()
    {
        ConfirmDeletionSettingDefinition definition = new();
        DeletionConfirmationService service = CreateService(definition);
        int eventCount = 0;
        service.ConfirmationRequirementChanged += (_, _) => eventCount++;

        service.SetConfirmationRequired(!definition.DefaultValue);

        service.IsConfirmationRequired.Should().Be(!definition.DefaultValue);
        eventCount.Should().Be(1);
    }

    private static DeletionConfirmationService CreateService(
        ConfirmDeletionSettingDefinition definition)
    {
        SettingsDefinitionCatalog catalog = new(
            [definition],
            Array.Empty<IUiScaleOptionDefinition>());

        return new DeletionConfirmationService(catalog);
    }
}
