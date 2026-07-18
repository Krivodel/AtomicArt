using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class WindowsFileActionsTests
{
    [Fact]
    public void GetOpenWithApplications_WithImageExtension_ReturnsUniqueHandlers()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        WindowsApplicationIconLoader iconLoader = new(
            NullLogger<WindowsApplicationIconLoader>.Instance);
        WindowsFileActions actions = new(iconLoader);

        IReadOnlyList<OpenWithApplication> applications =
            actions.GetOpenWithApplications("image.png");

        applications.Select(application => application.Identifier)
            .Should()
            .OnlyHaveUniqueItems();
        applications.Should().OnlyContain(application =>
            !string.IsNullOrWhiteSpace(application.Identifier)
                && !string.IsNullOrWhiteSpace(application.DisplayName));
    }
}
