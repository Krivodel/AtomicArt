using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.TestDoubles;

namespace AtomicArt.Desktop.Tests.Services.State;

public sealed class StateWriteSchedulerTests
{
    [Fact]
    public async Task ScheduleWrite_WithImmediateWriteAfterDeferredWrite_SavesOnlyImmediateState()
    {
        RecordingAppStateStore stateStore = new();
        NonDeserializingTestStateSection section = new();
        IStateWriteScheduler scheduler = new StateWriteScheduler(
            stateStore,
            NullLogger<StateWriteScheduler>.Instance,
            TimeSpan.FromMilliseconds(25));

        scheduler.ScheduleWrite(section, new TestState("old"));
        scheduler.ScheduleWrite(section, new TestState("new"), StateWriteMode.Immediate);
        await Task.Delay(75);
        await scheduler.FlushAsync(CancellationToken.None);

        stateStore.SavedStates.Should().ContainSingle()
            .Which.Value.Should().Be("new");
    }

    [Fact]
    public async Task FlushAsync_WithPendingWrites_WritesAllSections()
    {
        RecordingAppStateStore stateStore = new();
        NonDeserializingTestStateSection firstSection = new();
        OtherTestStateSection secondSection = new();
        IStateWriteScheduler scheduler = new StateWriteScheduler(
            stateStore,
            NullLogger<StateWriteScheduler>.Instance,
            TimeSpan.FromHours(1));

        scheduler.ScheduleWrite(firstSection, new TestState("first"));
        scheduler.ScheduleWrite(secondSection, new OtherTestState("second"));
        await scheduler.FlushAsync(CancellationToken.None);

        stateStore.SavedStates.Should().ContainSingle()
            .Which.Value.Should().Be("first");
        stateStore.OtherSavedStates.Should().ContainSingle()
            .Which.Value.Should().Be("second");
    }

    private sealed class RecordingAppStateStore : IAppStateStore
    {
        private readonly object _syncRoot = new();
        private readonly List<TestState> _savedStates = [];
        private readonly List<OtherTestState> _otherSavedStates = [];

        public IReadOnlyList<TestState> SavedStates
        {
            get
            {
                lock (_syncRoot)
                {
                    return _savedStates.ToList();
                }
            }
        }

        public IReadOnlyList<OtherTestState> OtherSavedStates
        {
            get
            {
                lock (_syncRoot)
                {
                    return _otherSavedStates.ToList();
                }
            }
        }

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
            lock (_syncRoot)
            {
                if (state is TestState testState)
                {
                    _savedStates.Add(testState);
                    return Task.CompletedTask;
                }

                if (state is OtherTestState otherTestState)
                {
                    _otherSavedStates.Add(otherTestState);
                    return Task.CompletedTask;
                }
            }

            throw new InvalidOperationException("Unexpected test state type.");
        }
    }

    private sealed class OtherTestStateSection : IStateSection
    {
        public string Key => "other-test";
        public string FileName => "other-test.json";
        public int SchemaVersion => 1;
        public Type PayloadType => typeof(OtherTestState);

        public object CreateDefaultPayload()
        {
            return new OtherTestState("default");
        }

        public object DeserializePayload(
            int schemaVersion,
            JsonElement payload,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException("State deserialization is not used by this test.");
        }
    }

    private sealed record OtherTestState(string Value);
}
