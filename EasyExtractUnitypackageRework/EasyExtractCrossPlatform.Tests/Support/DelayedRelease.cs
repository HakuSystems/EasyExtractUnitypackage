namespace EasyExtractCrossPlatform.Tests.Support;

/// <summary>
///     Releases a file lock after a delay from a dedicated thread. Tests that verify retry-on-lock
///     behaviour used Task.Run for this, which made them depend on thread pool availability: on a
///     small CI runner with tests in parallel the release could land after the retry budget had run
///     out, failing a test that had nothing wrong with it.
/// </summary>
internal static class DelayedRelease
{
    public static IDisposable After(TimeSpan delay, IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var released = new ManualResetEventSlim(false);
        var cancel = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            cancel.Wait(delay);

            try
            {
                resource.Dispose();
            }
            finally
            {
                released.Set();
            }
        })
        {
            IsBackground = true,
            Name = "DelayedRelease"
        };

        thread.Start();

        return new Handle(released, cancel);
    }

    private sealed class Handle(ManualResetEventSlim released, ManualResetEventSlim cancel) : IDisposable
    {
        public void Dispose()
        {
            // Bring the release forward so a failing test does not linger, then wait for it to land
            // before the test tears the directory down.
            cancel.Set();
            released.Wait(TimeSpan.FromSeconds(30));
            released.Dispose();
            cancel.Dispose();
        }
    }
}
