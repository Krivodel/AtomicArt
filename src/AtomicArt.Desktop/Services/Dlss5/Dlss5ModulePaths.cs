using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Dlss5;

public sealed class Dlss5ModulePaths
{
    public string ModuleDirectory => Path.Combine(
        _pathProvider.ModulesDirectory,
        Dlss5FeatureDefinition.ModuleDirectoryName);
    public string RuntimeDirectory => Path.Combine(ModuleDirectory, "runtime");
    public string HostDirectory => Path.Combine(RuntimeDirectory, "host");
    public string DlssnrDirectory => Path.Combine(RuntimeDirectory, "dlssnr");
    public string DlssDirectory => Path.Combine(RuntimeDirectory, "dlss");
    public string WorkerPath => Path.Combine(HostDirectory, "nvngx.dll");
    public string HostDxgiPath => Path.Combine(HostDirectory, "dxgi.dll");
    public string AddonPath => Path.Combine(DlssnrDirectory, "renodx-dlss5.addon64");
    public string NeuralRuntimePath => Path.Combine(DlssnrDirectory, "nvngx_dlssnr.dll");
    public string SuperResolutionPath => Path.Combine(DlssDirectory, "nvngx_dlss.dll");
    public string ReShadeLogPath => Path.Combine(HostDirectory, "ReShade.log");
    public string ReShadeIniPath => Path.Combine(HostDirectory, "ReShade.ini");
    public string DynamicAddonPath => Path.Combine(
        DlssnrDirectory,
        "dlss5-event-dynamic.addon64");

    private readonly IAtomicArtDataPathProvider _pathProvider;

    public Dlss5ModulePaths(IAtomicArtDataPathProvider pathProvider)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
    }
}
