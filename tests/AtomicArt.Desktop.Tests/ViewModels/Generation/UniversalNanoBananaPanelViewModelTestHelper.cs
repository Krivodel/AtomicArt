using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Tests.ViewModels.Generation;

internal static class UniversalNanoBananaPanelViewModelTestHelper
{
    public static ImageModelOption GetSelectedModel(UniversalNanoBananaPanelViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return viewModel.SelectedModel
            ?? throw new InvalidOperationException("Selected model is required for this test.");
    }
}
