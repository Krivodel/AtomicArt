using Microsoft.Extensions.Logging;

using Moq;

namespace AtomicArt.Desktop.Tests.Common;

internal static class LoggerMockAssertions
{
    public static void VerifyLog<T>(
        Mock<ILogger<T>> loggerMock,
        LogLevel level,
        Times times,
        string? expectedMessage = null,
        Func<Exception?, bool>? exceptionPredicate = null)
    {
        ArgumentNullException.ThrowIfNull(loggerMock);

        if (expectedMessage is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedMessage);
        }

        loggerMock.Verify(
            logger => logger.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    string.IsNullOrEmpty(expectedMessage)
                    || (state.ToString() ?? string.Empty).Contains(
                        expectedMessage,
                        StringComparison.Ordinal)),
                It.Is<Exception?>(exception =>
                    ReferenceEquals(exceptionPredicate, null)
                    || exceptionPredicate(exception)),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
