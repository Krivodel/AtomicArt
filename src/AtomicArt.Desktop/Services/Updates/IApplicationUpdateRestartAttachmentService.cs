using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Updates;

public interface IApplicationUpdateRestartAttachmentService
{
    void Attach(IAppStateFlushTarget stateFlushTarget);
}
