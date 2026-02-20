namespace EasyExtract.Core;

public sealed class NoopDisposable : IDisposable
{
    public static readonly NoopDisposable Instance = new();

    private NoopDisposable()
    {
    }

    public void Dispose()
    {
    }
}