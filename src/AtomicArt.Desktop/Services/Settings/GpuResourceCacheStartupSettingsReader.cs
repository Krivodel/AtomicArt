using System.Text.Json;

using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Settings;

public static class GpuResourceCacheStartupSettingsReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static long LoadMaxGpuResourceSizeBytes()
    {
        string? value = LoadSavedValueOrDefault();

        return GpuResourceCacheSettingOptions.ResolveBytes(value);
    }

    public static string? LoadSavedValueOrDefault()
    {
        try
        {
            AtomicArtDataPathProvider pathProvider = new();
            string settingsPath = Path.Combine(pathProvider.StateDirectory, SettingsStateSection.SectionFileName);

            if (!File.Exists(settingsPath))
            {
                return null;
            }

            using FileStream stream = File.OpenRead(settingsPath);
            StateEnvelope<SettingsState>? envelope =
                JsonSerializer.Deserialize<StateEnvelope<SettingsState>>(stream, JsonOptions);

            if (envelope?.Payload.Values is null)
            {
                return null;
            }

            return envelope.Payload.Values.TryGetValue(
                GpuResourceCacheSettingDefinition.SettingKey,
                out string? value)
                    ? value
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
