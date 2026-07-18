using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Tests.Services.State;

public sealed class AppStateBootstrapperTests
{
    private static readonly Guid GalleryItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task RestoreAsync_WithSavedState_RestoresSettingsPanelsAndGalleryInOrder()
    {
        List<string> calls = [];
        RecordingSettingsStateService settingsStateService = new(calls);
        RecordingGalleryStateService galleryStateService = new(calls);
        RecordingRestoreTarget target = new(calls);
        AppStateBootstrapper bootstrapper = new(
            settingsStateService,
            galleryStateService,
            new NoOpStateWriteScheduler(),
            NullLogger<AppStateBootstrapper>.Instance);

        await bootstrapper.RestoreAsync(target, CancellationToken.None);

        calls.Should().Equal(
            "settings.apply",
            "panel.restore:nano-banana",
            "gallery.load",
            "gallery.restore");
        target.RestoredGalleryItems.Should().ContainSingle()
            .Which.Id.Should().Be(GalleryItemId);
    }

    [Fact]
    public async Task RestoreAsync_WhenSettingsThrows_LogsErrorAndRestoresOtherSections()
    {
        List<string> calls = [];
        RecordingSettingsStateService settingsStateService = new(calls)
        {
            ThrowOnApply = true
        };
        RecordingGalleryStateService galleryStateService = new(calls);
        RecordingRestoreTarget target = new(calls);
        RecordingLogger<AppStateBootstrapper> logger = new RecordingLogger<AppStateBootstrapper>();
        AppStateBootstrapper bootstrapper = new(
            settingsStateService,
            galleryStateService,
            new NoOpStateWriteScheduler(),
            logger);

        await bootstrapper.RestoreAsync(target, CancellationToken.None);

        calls.Should().Equal(
            "settings.apply",
            "panel.restore:nano-banana",
            "gallery.load",
            "gallery.restore");
        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Error
            && entry.Message.Contains("settings", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FlushAsync_WithPendingWrite_SavesDeferredState()
    {
        RecordingAppStateStore stateStore = new();
        IStateWriteScheduler scheduler = new StateWriteScheduler(
            stateStore,
            NullLogger<StateWriteScheduler>.Instance,
            TimeSpan.FromHours(1));
        TestStateSection section = new();
        AppStateBootstrapper bootstrapper = new(
            new RecordingSettingsStateService([]),
            new RecordingGalleryStateService([]),
            scheduler,
            NullLogger<AppStateBootstrapper>.Instance);

        scheduler.ScheduleWrite(section, new TestState("prompt"));
        await bootstrapper.FlushAsync(new NoOpFlushTarget(), CancellationToken.None);

        stateStore.SavedStates.Should().ContainSingle()
            .Which.Value.Should().Be("prompt");
    }

    [Fact]
    public async Task FlushAsync_WithPendingPrompt_CommitsBeforeFlushingScheduler()
    {
        RecordingAppStateStore stateStore = new();
        IStateWriteScheduler scheduler = new StateWriteScheduler(
            stateStore,
            NullLogger<StateWriteScheduler>.Instance,
            TimeSpan.FromHours(1));
        TestStateSection section = new();
        RecordingFlushTarget target = new(
            () => scheduler.ScheduleWrite(section, new TestState("prompt")));
        AppStateBootstrapper bootstrapper = new(
            new RecordingSettingsStateService([]),
            new RecordingGalleryStateService([]),
            scheduler,
            NullLogger<AppStateBootstrapper>.Instance);

        await bootstrapper.FlushAsync(target, CancellationToken.None);

        target.CommitCallCount.Should().Be(1);
        stateStore.SavedStates.Should().ContainSingle()
            .Which.Value.Should().Be("prompt");
    }

    private sealed class RecordingSettingsStateService : ISettingsStateService
    {
        private readonly List<string> _calls;

        public bool ThrowOnApply { get; init; }

        public RecordingSettingsStateService(List<string> calls)
        {
            _calls = calls ?? throw new ArgumentNullException(nameof(calls));
        }

        public Task ApplySavedSettingsAsync(CancellationToken ct)
        {
            _calls.Add("settings.apply");

            if (ThrowOnApply)
            {
                throw new InvalidOperationException("Settings restore failed.");
            }

            return Task.CompletedTask;
        }

        public void ApplyValue(ISettingsDefinition definition, string value)
        {
            throw new NotSupportedException("Applying a setting value is not used by this test.");
        }

        public Task<string?> LoadValueAsync(ISettingsDefinition definition, CancellationToken ct)
        {
            throw new NotSupportedException("Settings value loading is not used by this test.");
        }

        public Task SaveValueAsync(ISettingsDefinition definition, string value, CancellationToken ct)
        {
            throw new NotSupportedException("Settings value saving is not used by this test.");
        }
    }

    private sealed class RecordingGalleryStateService : IGalleryStateService
    {
        private readonly List<string> _calls;

        public RecordingGalleryStateService(List<string> calls)
        {
            _calls = calls ?? throw new ArgumentNullException(nameof(calls));
        }

        public Task<GalleryState> LoadAsync(CancellationToken ct)
        {
            _calls.Add("gallery.load");
            GalleryState state = new()
            {
                Items = new List<GalleryItemState>
                {
                    new()
                    {
                        Id = GalleryItemId,
                        ModelId = "model",
                        ModelDisplayName = "Model",
                        Prompt = "prompt",
                        AspectRatio = "1:1",
                        Resolution = "1024x1024",
                        CreatedAtUtc = new DateTime(2026, 7, 6, 8, 0, 0, DateTimeKind.Utc),
                        Status = GenerationItemStatus.Generated
                    }
                }
            };

            return Task.FromResult(state);
        }

        public Task SaveAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct)
        {
            throw new NotSupportedException("Gallery state saving is not used by this test.");
        }
    }

    private sealed class RecordingRestoreTarget : IAppStateRestoreTarget
    {
        private readonly List<string> _calls;

        public IReadOnlyList<GalleryItemState> RestoredGalleryItems { get; private set; } =
            [];

        public RecordingRestoreTarget(List<string> calls)
        {
            _calls = calls ?? throw new ArgumentNullException(nameof(calls));
        }

        public Task RestoreGenerationPanelsAsync(CancellationToken ct)
        {
            _calls.Add("panel.restore:nano-banana");
            return Task.CompletedTask;
        }

        public Task RestoreGalleryAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(items);

            _calls.Add("gallery.restore");
            RestoredGalleryItems = items.ToList();

            return Task.CompletedTask;
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
            throw new NotSupportedException("Scheduling writes is not used by this test.");
        }

        public Task FlushAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpFlushTarget : IAppStateFlushTarget
    {
        public Task CommitPendingStateAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingFlushTarget : IAppStateFlushTarget
    {
        private readonly Action _commit;

        public int CommitCallCount { get; private set; }

        public RecordingFlushTarget(Action commit)
        {
            _commit = commit ?? throw new ArgumentNullException(nameof(commit));
        }

        public Task CommitPendingStateAsync(CancellationToken ct)
        {
            CommitCallCount++;
            _commit();

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAppStateStore : IAppStateStore
    {
        private readonly List<TestState> _savedStates = [];

        public IReadOnlyList<TestState> SavedStates => _savedStates.ToList();

        public Task<TState> LoadAsync<TState>(IStateSection section, CancellationToken ct)
        {
            throw new NotSupportedException("State loading is not used by this test.");
        }

        public Task SaveAsync<TState>(IStateSection section, TState state, CancellationToken ct)
            where TState : notnull
        {
            return SaveAsync(section, (object)state, ct);
        }

        public Task SaveAsync(IStateSection section, object state, CancellationToken ct)
        {
            if (state is not TestState testState)
            {
                throw new InvalidOperationException("Unexpected test state type.");
            }

            _savedStates.Add(testState);

            return Task.CompletedTask;
        }
    }

    private sealed class TestStateSection : IStateSection
    {
        public string Key => "test";
        public string FileName => "test.json";
        public int SchemaVersion => 1;
        public Type PayloadType => typeof(TestState);

        public object CreateDefaultPayload()
        {
            return new TestState("default");
        }

        public object DeserializePayload(
            int schemaVersion,
            JsonElement payload,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException("State deserialization is not used by this test.");
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries => _entries.ToList();

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed record TestState(string Value);

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
