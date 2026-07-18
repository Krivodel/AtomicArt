using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.State;

public sealed class AppStateBootstrapperTests
{
    private const string SavedPrompt = "prompt";
    private static readonly Guid GalleryItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task RestoreAsync_WithSavedState_RestoresSettingsPanelsAndGalleryInOrder()
    {
        RestoreTestContext context = new();

        await context.Bootstrapper.RestoreAsync(context.Target, CancellationToken.None);

        AssertRestoreCalls(context.Calls);
        context.Target.RestoredGalleryItems.Should().ContainSingle()
            .Which.Id.Should().Be(GalleryItemId);
    }

    [Fact]
    public async Task RestoreAsync_WhenSettingsThrows_LogsErrorAndRestoresOtherSections()
    {
        RecordingLogger<AppStateBootstrapper> logger = new RecordingLogger<AppStateBootstrapper>();
        RestoreTestContext context = new(logger);
        context.SettingsStateService.EnableApplyFailure();

        await context.Bootstrapper.RestoreAsync(context.Target, CancellationToken.None);

        AssertRestoreCalls(context.Calls);
        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Error
            && entry.Message.Contains("settings", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FlushAsync_WithPendingWrite_SavesDeferredState()
    {
        FlushTestContext context = new();

        context.SchedulePromptWrite();
        await context.Bootstrapper.FlushAsync(new NoOpFlushTarget(), CancellationToken.None);

        context.AssertPromptSaved();
    }

    [Fact]
    public async Task FlushAsync_WithPendingPrompt_CommitsBeforeFlushingScheduler()
    {
        FlushTestContext context = new();
        RecordingFlushTarget target = new(context.SchedulePromptWrite);

        await context.Bootstrapper.FlushAsync(target, CancellationToken.None);

        target.CommitCallCount.Should().Be(1);
        context.AssertPromptSaved();
    }

    private static void AssertRestoreCalls(List<string> calls)
    {
        calls.Should().Equal(
            "settings.apply",
            "panel.restore:nano-banana",
            "gallery.load",
            "gallery.restore");
    }

    private sealed class RestoreTestContext
    {
        public List<string> Calls { get; }
        public RecordingSettingsStateService SettingsStateService { get; }
        public RecordingRestoreTarget Target { get; }
        public AppStateBootstrapper Bootstrapper { get; }

        public RestoreTestContext(ILogger<AppStateBootstrapper>? logger = null)
        {
            Calls = [];
            SettingsStateService = new RecordingSettingsStateService(Calls);
            RecordingGalleryStateService galleryStateService = new(Calls);
            Target = new RecordingRestoreTarget(Calls);
            Bootstrapper = new AppStateBootstrapper(
                SettingsStateService,
                galleryStateService,
                new NoOpStateWriteScheduler(),
                logger ?? NullLogger<AppStateBootstrapper>.Instance);
        }
    }

    private sealed class FlushTestContext
    {
        public AppStateBootstrapper Bootstrapper { get; }

        private readonly RecordingAppStateStore _stateStore;
        private readonly IStateWriteScheduler _scheduler;
        private readonly NonDeserializingTestStateSection _section;

        public FlushTestContext()
        {
            _stateStore = new RecordingAppStateStore(typeof(TestState));
            _scheduler = new StateWriteScheduler(
                _stateStore,
                NullLogger<StateWriteScheduler>.Instance,
                TimeSpan.FromHours(1));
            _section = new NonDeserializingTestStateSection();
            Bootstrapper = new AppStateBootstrapper(
                new RecordingSettingsStateService([]),
                new RecordingGalleryStateService([]),
                _scheduler,
                NullLogger<AppStateBootstrapper>.Instance);
        }

        public void SchedulePromptWrite()
        {
            _scheduler.ScheduleWrite(_section, new TestState(SavedPrompt));
        }

        public void AssertPromptSaved()
        {
            _stateStore.GetSavedStates<TestState>().Should().ContainSingle()
                .Which.Value.Should().Be(SavedPrompt);
        }
    }

    private abstract class CallRecordingTestDouble
    {
        protected List<string> Calls { get; }

        protected CallRecordingTestDouble(List<string> calls)
        {
            Calls = calls ?? throw new ArgumentNullException(nameof(calls));
        }
    }

    private sealed class RecordingSettingsStateService
        : CallRecordingTestDouble, ISettingsStateService
    {
        public bool ThrowOnApply { get; private set; }

        public RecordingSettingsStateService(List<string> calls)
            : base(calls)
        {
        }

        public void EnableApplyFailure()
        {
            ThrowOnApply = true;
        }

        public Task ApplySavedSettingsAsync(CancellationToken ct)
        {
            Calls.Add("settings.apply");

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

    private sealed class RecordingGalleryStateService
        : CallRecordingTestDouble, IGalleryStateService
    {
        public RecordingGalleryStateService(List<string> calls)
            : base(calls)
        {
        }

        public Task<GalleryState> LoadAsync(CancellationToken ct)
        {
            Calls.Add("gallery.load");
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
                        Resolution = TestGenerationOutputMetadata.GeneratedImageResolution,
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

    private sealed class RecordingRestoreTarget
        : CallRecordingTestDouble, IAppStateRestoreTarget
    {
        public IReadOnlyList<GalleryItemState> RestoredGalleryItems { get; private set; } =
            [];

        public RecordingRestoreTarget(List<string> calls)
            : base(calls)
        {
        }

        public Task RestoreGenerationPanelsAsync(CancellationToken ct)
        {
            Calls.Add("panel.restore:nano-banana");
            return Task.CompletedTask;
        }

        public Task RestoreGalleryAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(items);

            Calls.Add("gallery.restore");
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

}
