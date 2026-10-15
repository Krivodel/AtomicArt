using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.Services.Settings;

internal sealed class ImageViewerWindowLimitSettingsTestContext
{
    public ImageViewerWindowLimitSettingDefinition Definition { get; } = new();
    public ImageViewerWindowLimitService Limit { get; }
    public ImageViewerWindowLimitSettingsStateApplicator Applicator { get; }
    public RecordingStateWriteScheduler Scheduler { get; } = new();
    public SettingsStateService Settings { get; }

    public ImageViewerWindowLimitSettingsTestContext(SettingsState state)
    {
        SettingsDefinitionCatalog definitions = new(
            new ISettingsDefinition[] { Definition }, Array.Empty<IUiScaleOptionDefinition>());
        Limit = new ImageViewerWindowLimitService(definitions);
        Applicator = new ImageViewerWindowLimitSettingsStateApplicator(Limit);
        Settings = new SettingsStateService(
            new StubAppStateStore(state),
            Scheduler,
            definitions,
            new SettingsStateSection(),
            new ISettingsStateApplicator[] { Applicator },
            new ImmediateUiThreadDispatcher());
    }

    public ImageViewerWindowLimitSettingViewModel CreateEditor()
    {
        ImageViewerWindowLimitSettingViewModelFactory factory = new(
            Limit, Settings, new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default);

        return factory.Create(Definition) as ImageViewerWindowLimitSettingViewModel
            ?? throw new InvalidOperationException("The window limit editor was not created.");
    }
}
