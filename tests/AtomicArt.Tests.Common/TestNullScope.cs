namespace AtomicArt.Tests.Common;

internal sealed class TestNullScope : IDisposable
{
    internal static TestNullScope Instance { get; } = new();

    private TestNullScope()
    {
    }

    public void Dispose()
    {
    }
}
