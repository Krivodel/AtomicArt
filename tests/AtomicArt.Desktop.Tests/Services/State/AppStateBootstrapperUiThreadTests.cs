using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Controls;
using Avalonia.Headless;

using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Services.State;

public sealed class AppStateBootstrapperUiThreadTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public async Task RestoreAsync_AfterBackgroundSettingsCompletion_UpdatesBoundCommandOnUiThread()
    {
        await DispatchAsync(async () =>
        {
            BoundCommandRestoreTarget target = new();
            Button button = new()
            {
                Command = target.Command
            };
            Window window = Show(button);
            AppStateBootstrapper bootstrapper = new(
                new BackgroundCompletingSettingsStateService(),
                new EmptyGalleryStateService(),
                new NoOpStateWriteScheduler(),
                new AvaloniaUiThreadDispatcher(),
                NullLogger<AppStateBootstrapper>.Instance);

            try
            {
                target.Command.CanExecute(null).Should().BeFalse();
                button.Command.Should().BeSameAs(target.Command);

                await bootstrapper.RestoreAsync(target, CancellationToken.None);
                window.CaptureRenderedFrame();

                target.GenerationPanelsRestored.Should().BeTrue();
                target.Command.CanExecute(null).Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private sealed class BoundCommandRestoreTarget : IAppStateRestoreTarget
    {
        public IRelayCommand Command { get; }
        public bool GenerationPanelsRestored { get; private set; }

        private bool _canExecute;

        public BoundCommandRestoreTarget()
        {
            Command = new RelayCommand(
                () => { },
                () => _canExecute);
        }

        public Task RestoreGenerationPanelsAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _canExecute = true;
            Command.NotifyCanExecuteChanged();
            GenerationPanelsRestored = true;

            return Task.CompletedTask;
        }

        public Task RestoreGalleryAsync(
            IReadOnlyList<GalleryItemState> items,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(items);
            ct.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }

    private sealed class BackgroundCompletingSettingsStateService : ISettingsStateService
    {
        public async Task ApplySavedSettingsAsync(CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), ct).ConfigureAwait(false);
        }

        public void ApplyValue(ISettingsDefinition definition, string value)
        {
            throw new NotSupportedException();
        }

        public Task<string?> LoadValueAsync(
            ISettingsDefinition definition,
            CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public Task SaveValueAsync(
            ISettingsDefinition definition,
            string value,
            CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class EmptyGalleryStateService : IGalleryStateService
    {
        public Task<GalleryState> LoadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            return Task.FromResult(new GalleryState());
        }

        public Task SaveAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NoOpStateWriteScheduler : IStateWriteScheduler
    {
        public void ScheduleWrite<TState>(
            IStateSection section,
            TState state,
            StateWriteMode mode = StateWriteMode.Deferred)
            where TState : notnull
        {
            throw new NotSupportedException();
        }

        public Task FlushAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
