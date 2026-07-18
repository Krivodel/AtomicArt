using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Services;

public sealed class DesktopModelPanelRegistry
{
    public IModelPanelViewModel GetDefaultPanel(IReadOnlyList<IModelPanelViewModel> panels)
    {
        ArgumentNullException.ThrowIfNull(panels);

        return panels.FirstOrDefault()
            ?? throw new InvalidOperationException("No desktop model panels are registered.");
    }

    public IModelPanelViewModel GetPanel(string modelId, IReadOnlyList<IModelPanelViewModel> panels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(panels);

        IModelPanelViewModel? panel = panels.FirstOrDefault(currentPanel =>
            currentPanel.SupportsModel(modelId));

        if (panel is not null)
        {
            return panel;
        }

        throw new InvalidOperationException("Desktop panel is not registered for selected model.");
    }
}
