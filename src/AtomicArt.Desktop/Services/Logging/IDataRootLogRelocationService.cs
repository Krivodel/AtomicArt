using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Logging;

public interface IDataRootLogRelocationService
{
    void Pause();
    void Resume(IAtomicArtDataPathProvider pathProvider);
}
