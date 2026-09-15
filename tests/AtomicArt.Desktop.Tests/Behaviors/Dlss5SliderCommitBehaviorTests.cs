using System.Windows.Input;

using Avalonia.Controls;
using Avalonia.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Behaviors;

namespace AtomicArt.Desktop.Tests.Behaviors;

public sealed class Dlss5SliderCommitBehaviorTests
{
    [Fact]
    public void SliderValueChanges_DoNotExecuteCommandUntilKeyboardCommit()
    {
        Slider slider = new();
        CountingCommand command = new();
        Dlss5SliderCommitBehavior.SetCommand(slider, command);

        slider.Value = 1d;

        command.ExecutionCount.Should().Be(0);

        slider.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyUpEvent,
            Key = Key.Right,
            PhysicalKey = PhysicalKey.ArrowRight
        });

        command.ExecutionCount.Should().Be(1);
    }

    private sealed class CountingCommand : ICommand
    {
        public int ExecutionCount { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add
            {
            }
            remove
            {
            }
        }

        public bool CanExecute(object? parameter)
        {
            _ = parameter;
            return true;
        }

        public void Execute(object? parameter)
        {
            _ = parameter;
            ExecutionCount++;
        }
    }
}
