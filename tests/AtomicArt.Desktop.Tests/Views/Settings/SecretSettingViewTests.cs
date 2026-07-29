using Avalonia.Controls;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Tests.Views.Settings;

public sealed class SecretSettingViewTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void Loaded_WithStoredSecret_LoadsMaskedFieldValue()
    {
        Dispatch(() =>
        {
            SecretSettingViewModel viewModel = new(
                new GoogleApiKeySettingDefinition(),
                new StoredSecretStore(),
                new TestViewModelErrorHandler(),
                TestLocalizationTextProvider.Default);
            SecretSettingView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                TextBox textBox = view
                    .GetVisualDescendants()
                    .OfType<TextBox>()
                    .Single();

                textBox.Text.Should().Be("stored-value-for-test-only");
                textBox.PasswordChar.Should().Be('*');
            }
            finally
            {
                window.Close();
            }
        });
    }

    private sealed class StoredSecretStore : ISecretStore
    {
        public Task<string?> GetSecretAsync(string key, CancellationToken ct)
        {
            return Task.FromResult<string?>("stored-value-for-test-only");
        }

        public Task SetSecretAsync(string key, string value, CancellationToken ct)
        {
            throw new NotSupportedException("Saving a secret is not used by this test.");
        }
    }
}
