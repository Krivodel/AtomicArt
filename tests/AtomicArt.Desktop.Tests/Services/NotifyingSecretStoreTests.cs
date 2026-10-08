using FluentAssertions;
using Xunit;

using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Desktop.Services;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class NotifyingSecretStoreTests
{
    [Fact]
    public async Task SetSecretAsync_WhenSaved_PublishesSecretNameAndPreservesProtectedStorage()
    {
        using TemporaryDirectory directory = new(typeof(NotifyingSecretStoreTests),
            nameof(SetSecretAsync_WhenSaved_PublishesSecretNameAndPreservesProtectedStorage));
        ProtectedDesktopSecretStore protectedStore = new(directory.DirectoryPath,
            TestApiConfiguration.CreateStorageOptionsWrapper(), TestApiConfiguration.CreateTrustedFileStreamFactory());
        IMessenger messenger = new WeakReferenceMessenger();
        object recipient = new();
        SecretChangedMessage? notification = null;
        messenger.Register<object, SecretChangedMessage>(recipient, (_, message) => notification = message);
        NotifyingSecretStore store = new(protectedStore, messenger);

        await store.SetSecretAsync(OpenRouterManagementKeySettingDefinition.SecretNameValue,
            "test-management-key", CancellationToken.None);

        notification?.Key.Should().Be(OpenRouterManagementKeySettingDefinition.SecretNameValue);
        (await store.GetSecretAsync(OpenRouterManagementKeySettingDefinition.SecretNameValue,
            CancellationToken.None)).Should().Be("test-management-key");
    }

    [Fact]
    public async Task SetSecretAsync_WhenSaveFails_DoesNotPublishChange()
    {
        using TemporaryDirectory directory = new(typeof(NotifyingSecretStoreTests),
            nameof(SetSecretAsync_WhenSaveFails_DoesNotPublishChange));
        ProtectedDesktopSecretStore protectedStore = new(directory.DirectoryPath,
            TestApiConfiguration.CreateStorageOptionsWrapper(), TestApiConfiguration.CreateTrustedFileStreamFactory());
        IMessenger messenger = new WeakReferenceMessenger();
        object recipient = new();
        bool wasNotified = false;
        messenger.Register<object, SecretChangedMessage>(recipient, (_, _) => wasNotified = true);
        NotifyingSecretStore store = new(protectedStore, messenger);

        Func<Task> action = () => store.SetSecretAsync(string.Empty, "value", CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
        wasNotified.Should().BeFalse();
    }
}
