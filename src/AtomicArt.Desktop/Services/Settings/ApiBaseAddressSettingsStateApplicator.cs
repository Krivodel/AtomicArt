namespace AtomicArt.Desktop.Services.Settings;

public sealed class ApiBaseAddressSettingsStateApplicator : ISettingsStateApplicator
{
    public string SettingKey { get; }

    private readonly IApiEndpointService _apiEndpointService;

    public ApiBaseAddressSettingsStateApplicator(
        ISettingsDefinitionCatalog settingsDefinitionCatalog,
        IApiEndpointService apiEndpointService)
    {
        ArgumentNullException.ThrowIfNull(settingsDefinitionCatalog);

        _apiEndpointService = apiEndpointService
            ?? throw new ArgumentNullException(nameof(apiEndpointService));
        SettingKey = settingsDefinitionCatalog
            .GetRequired<ApiBaseAddressSettingDefinition>()
            .Key;
    }

    public void Apply(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!ApiBaseAddress.TryCreate(value, out ApiBaseAddress? baseAddress)
            || baseAddress is null)
        {
            return;
        }

        _apiEndpointService.SetBaseAddress(baseAddress);
    }
}
