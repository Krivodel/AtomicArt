using FluentAssertions;
using Moq;
using Xunit;

using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.OpenRouter;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels;

namespace AtomicArt.Desktop.Tests.ViewModels;

public sealed class OpenRouterBalanceViewModelTests
{
    private static readonly Guid CorrelationId = Guid.Parse("42222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task StartMonitoringCommand_WithManagementKey_LoadsBalanceAtStartup()
    {
        using BalanceTestContext context = new();

        await context.ViewModel.StartMonitoringCommand.ExecuteAsync(null);

        context.ViewModel.BalanceText.Should().Be("Balance: $1234.56");
        context.ViewModel.HasManagementKey.Should().BeTrue();
        context.ApiClient.Verify(client => client.GetBalanceAsync("management-key", It.IsAny<CancellationToken>()), Times.Once);
        context.ViewModel.StartMonitoringCommand.CanExecute(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefreshAsync_WithoutManagementKey_HidesBalanceAndDoesNotRequestIt(string? managementKey)
    {
        using BalanceTestContext context = new();
        context.ManagementKey = managementKey;

        await context.ViewModel.RefreshAsync(CancellationToken.None);

        context.ViewModel.Balance.Should().BeNull();
        context.ViewModel.HasManagementKey.Should().BeFalse();
        context.ViewModel.BalanceText.Should().Be("Balance: —");
        context.ApiClient.Verify(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_WithProviderFailure_PreservesLastBalanceAndLogsSafeError()
    {
        using BalanceTestContext context = new();
        await context.ViewModel.RefreshAsync(CancellationToken.None);
        context.ApiClient.Setup(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("private provider details"));

        await context.ViewModel.RefreshAsync(CancellationToken.None);

        context.ViewModel.Balance.Should().Be(1234.56m);
        context.ViewModel.HasManagementKey.Should().BeTrue();
        context.ViewModel.ErrorMessage.Should().NotBeNullOrWhiteSpace().And.NotContain("private provider details");
        context.ErrorHandler.LogCallCount.Should().Be(1);
        context.ViewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshAsync_WithOverlappingRequests_CoalescesAndPerformsTrailingRefresh()
    {
        using BalanceTestContext context = new();
        TaskCompletionSource<decimal> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context.ApiClient.SetupSequence(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(pending.Task)
            .ReturnsAsync(42m);

        Task first = context.ViewModel.RefreshAsync(CancellationToken.None);
        Task second = context.ViewModel.RefreshAsync(CancellationToken.None);
        Task third = context.ViewModel.RefreshAsync(CancellationToken.None);
        context.ApiClient.Verify(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        pending.SetResult(100m);
        await Task.WhenAll(first, second, third);

        context.ApiClient.Verify(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        context.ViewModel.Balance.Should().Be(42m);
    }

    [Fact]
    public async Task SecretChangedMessage_DuringRequest_DiscardsOldAccountBalance()
    {
        using BalanceTestContext context = new();
        TaskCompletionSource<decimal> oldResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<decimal> newResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource newRequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context.ApiClient.Setup(client => client.GetBalanceAsync("management-key", It.IsAny<CancellationToken>()))
            .Returns(oldResponse.Task);
        context.ApiClient.Setup(client => client.GetBalanceAsync("new-key", It.IsAny<CancellationToken>()))
            .Callback(() => newRequestStarted.SetResult())
            .Returns(newResponse.Task);
        Task refresh = context.ViewModel.RefreshAsync(CancellationToken.None);

        context.ManagementKey = "new-key";
        context.Messenger.Send(new SecretChangedMessage(OpenRouterManagementKeySettingDefinition.SecretNameValue));
        oldResponse.SetResult(999m);
        await newRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        context.ViewModel.Balance.Should().BeNull();
        newResponse.SetResult(7m);
        await refresh;

        context.ViewModel.Balance.Should().Be(7m);
    }

    [Fact]
    public async Task SecretChangedMessage_WhenManagementKeyRemoved_ClearsAndHidesPreviousBalance()
    {
        using BalanceTestContext context = new();
        await context.ViewModel.RefreshAsync(CancellationToken.None);
        context.ManagementKey = string.Empty;

        context.Messenger.Send(new SecretChangedMessage(OpenRouterManagementKeySettingDefinition.SecretNameValue));

        context.ViewModel.Balance.Should().BeNull();
        context.ViewModel.HasManagementKey.Should().BeFalse();
        context.ApiClient.Verify(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SecretChangedMessage_ForGenerationKey_DoesNotRefreshBalance()
    {
        using BalanceTestContext context = new();

        context.Messenger.Send(new SecretChangedMessage(OpenRouterApiKeySettingDefinition.SecretNameValue));

        context.ApiClient.Verify(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(GenerationLifecycleStatus.Completed)]
    [InlineData(GenerationLifecycleStatus.Failed)]
    [InlineData(GenerationLifecycleStatus.StartFailed)]
    public void GenerationFinished_WithTerminalStatus_RefreshesEvenWithoutWindowFocus(GenerationLifecycleStatus status)
    {
        using BalanceTestContext context = new();
        GenerationBatchDto? batch = status == GenerationLifecycleStatus.Completed
            ? new GenerationBatchDto(CorrelationId, new List<GenerationItemDto>())
            : null;

        context.GenerationEvents.Publish(new GenerationLifecycleEvent(CorrelationId, status, null, batch, null));

        context.ApiClient.Verify(client => client.GetBalanceAsync("management-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Monitor_WithFocus_RefreshesEveryThirtySecondsAndPausesWithoutFocus()
    {
        using BalanceTestContext context = new();
        context.ViewModel.SetWindowActive(true);
        await context.ViewModel.StartMonitoringCommand.ExecuteAsync(null);
        context.Clock.Verify(clock => clock.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object?>(),
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)), Times.Once);

        await context.TickAsync();
        context.ViewModel.SetWindowActive(false);
        await context.TickAsync();
        await context.TickAsync();

        context.ApiClient.Verify(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SetWindowActive_AfterRestoringWindow_RefreshesImmediately()
    {
        using BalanceTestContext context = new();
        await context.ViewModel.StartMonitoringCommand.ExecuteAsync(null);

        context.ViewModel.SetWindowActive(true);
        context.ViewModel.SetWindowActive(true);

        context.ApiClient.Verify(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Dispose_WithPendingRequest_CancelsAndUnsubscribes()
    {
        using BalanceTestContext context = new();
        TaskCompletionSource<decimal> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context.ApiClient.Setup(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((_, ct) => pending.Task.WaitAsync(ct));
        Task startup = context.ViewModel.StartMonitoringCommand.ExecuteAsync(null);

        context.ViewModel.Dispose();
        await startup;
        context.GenerationEvents.Publish(new GenerationLifecycleEvent(CorrelationId,
            GenerationLifecycleStatus.Failed, null, null, null));
        context.Messenger.Send(new SecretChangedMessage(OpenRouterManagementKeySettingDefinition.SecretNameValue));

        context.ApiClient.Verify(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        context.Timer.Verify(timer => timer.Dispose(), Times.Once);
        context.ErrorHandler.LogCallCount.Should().Be(0);
    }

    [Fact]
    public async Task LocalizationChangedMessage_WithBalance_RefreshesHeaderText()
    {
        Dictionary<string, string> strings = new()
        {
            [ShellLocalizationKeys.OpenRouterBalanceFormat] = "Balance: ${0}"
        };
        using BalanceTestContext context = new(new TestLocalizationTextProvider(strings));
        await context.ViewModel.RefreshAsync(CancellationToken.None);
        strings[ShellLocalizationKeys.OpenRouterBalanceFormat] = "Баланс: ${0}";

        context.Messenger.Send(new LocalizationChangedMessage());

        context.ViewModel.BalanceText.Should().Be("Баланс: $1234.56");
    }

    private sealed class BalanceTestContext : IDisposable
    {
        public string? ManagementKey { get; set; } = "management-key";
        public Mock<IOpenRouterBalanceApiClient> ApiClient { get; } = new();
        public Mock<TimeProvider> Clock { get; } = new();
        public Mock<ITimer> Timer { get; } = new();
        public TestViewModelErrorHandler ErrorHandler { get; } = new();
        public IMessenger Messenger { get; } = new WeakReferenceMessenger();
        public TestGenerationLifecycleEventHub GenerationEvents { get; } = new();
        public OpenRouterBalanceViewModel ViewModel { get; }

        private Action? _tick;
        private TaskCompletionSource? _tickDispatched;

        public BalanceTestContext(ILocalizationTextProvider? textProvider = null)
        {
            Mock<ISecretStore> store = new();
            store.Setup(service => service.GetSecretAsync(OpenRouterManagementKeySettingDefinition.SecretNameValue,
                It.IsAny<CancellationToken>())).ReturnsAsync(() => ManagementKey);
            ApiClient.Setup(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1234.56m);
            Clock.Setup(clock => clock.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object?>(),
                It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
                .Callback<TimerCallback, object?, TimeSpan, TimeSpan>((callback, state, _, _) =>
                    _tick = () => callback(state))
                .Returns(Timer.Object);
            Mock<IUiThreadDispatcher> dispatcher = new();
            dispatcher.Setup(service => service.InvokeAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns<Func<Task>, CancellationToken>(async (action, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    await action();
                    _tickDispatched?.TrySetResult();
                });
            ViewModel = new OpenRouterBalanceViewModel(ApiClient.Object, store.Object, GenerationEvents,
                dispatcher.Object, ErrorHandler, textProvider ?? TestLocalizationTextProvider.Default,
                Clock.Object, Messenger);
        }

        public async Task TickAsync()
        {
            _tickDispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Action tick = _tick ?? throw new InvalidOperationException("Balance timer was not started.");
            tick();
            await _tickDispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            ViewModel.Dispose();
        }
    }
}
