using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using NAudio.Wave;
using Xunit;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Notifications;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Rendering.Tests.Views.Notifications;

public sealed partial class GenerationNotificationTests : DesktopControlTestBase
{
    public new static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<DesktopControlTestApplication>()
            .UseSkia()
            .UseHarfBuzz()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    [Fact]
    public async Task DefaultSound_WithBundledAsset_DecodesMp3Audio()
    {
        await DispatchAsync(typeof(GenerationNotificationTests), () =>
        {
            using Stream source = AssetLoader.Open(new Uri("avares://AtomicArt/Assets/Notification.mp3"));
            using MemoryStream content = new();
            source.CopyTo(content);
            using WaveStream reader = NotificationSoundDecoder.Open(content.ToArray());
            byte[] samples = new byte[4096];

            int read = reader.Read(samples, 0, samples.Length);

            reader.TotalTime.Should().BeGreaterThan(TimeSpan.Zero);
            read.Should().BeGreaterThan(0);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Settings_WithNotifications_AlignsTheSwitchWithOtherSettings(bool hasCustomSound)
    {
        await DispatchAsync(typeof(GenerationNotificationTests), async () =>
        {
            Mock<INotificationSoundStore> sounds = new();
            sounds.SetupGet(store => store.HasCustomSound).Returns(hasCustomSound);
            Mock<ISettingsStateService> state = new();
            state.Setup(service => service.SaveValueAsync(It.IsAny<ISettingsDefinition>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Mock<ISoundFilePickerService> picker = new();
            picker.Setup(service => service.PickSoundAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
            using GenerationNotificationsSettingViewModel editor = new(new GenerationNotificationsSettingDefinition(),
                new GenerationNotificationSettings(), state.Object,
                new BooleanSettingValueConverter(), sounds.Object, picker.Object,
                new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default);
            GenerationNotificationsSettingView view = new() { DataContext = editor };
            Window window = Show(view, 640, 150);
            try
            {
                CheckBox toggle = view.GetVisualDescendants().OfType<CheckBox>().Single();
                Button upload = view.GetVisualDescendants().OfType<Button>()
                    .Single(button => button.Command == editor.UploadSoundCommand);
                Point? togglePosition = toggle.TranslatePoint(default, window);
                Point? uploadPosition = upload.TranslatePoint(default, window);

                toggle.IsChecked.Should().BeTrue();
                upload.Content.Should().Be(TestLocalizationTextProvider.Default.Get(NotificationLocalizationKeys.UploadSound));
                togglePosition.Should().NotBeNull();
                uploadPosition.Should().NotBeNull();
                if (togglePosition is Point togglePoint && uploadPosition is Point uploadPoint)
                {
                    Math.Abs(togglePoint.Y + toggle.Bounds.Height / 2 - uploadPoint.Y - upload.Bounds.Height / 2)
                        .Should().BeLessThan(1);
                    (uploadPoint.X + upload.Bounds.Width).Should().BeLessThan(togglePoint.X);
                    (togglePoint.X + toggle.Bounds.Width).Should().BeApproximately(view.Bounds.Width, 1);
                }

                SaveFrame(window, hasCustomSound ? "notification-settings-custom.png" : "notification-settings.png");
                Button label = view.GetVisualDescendants().OfType<Button>()
                    .Single(button => button.Command == editor.ToggleCommand);
                ClickButton(window, upload);
                await (editor.UploadSoundCommand.ExecutionTask ?? Task.CompletedTask);
                editor.IsChecked.Should().BeTrue();
                picker.Verify(service => service.PickSoundAsync(It.IsAny<CancellationToken>()), Times.Once);

                ClickButton(window, label);
                await (editor.ApplyCommand.ExecutionTask ?? Task.CompletedTask);
                editor.IsChecked.Should().BeFalse();
                state.Verify(service => service.SaveValueAsync(It.IsAny<ISettingsDefinition>(), "False",
                    It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void ClickButton(Window window, Button button)
    {
        Point point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("The settings button is not attached to the test window.");
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static void SaveFrame(Window window, string fileName)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "NotificationVisualChecks");
        Directory.CreateDirectory(directory);
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        frame?.Save(Path.Combine(directory, fileName));
    }
}
