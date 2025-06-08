namespace EasyExtract.Utilities.Logger;

public class LogScope : IDisposable
{
    private readonly string _category;
    private readonly string _scopeName;
    private readonly Stopwatch _stopwatch;

    public LogScope(string scopeName, string category)
    {
        _scopeName = scopeName;
        _category = category;
        _stopwatch = Stopwatch.StartNew();
        BetterLogger.Debug($"Entering scope: {scopeName}", category);
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        BetterLogger.LogWithContext($"Exiting scope: {_scopeName}", new Dictionary<string, object>
        {
            ["Duration"] = $"{_stopwatch.ElapsedMilliseconds}ms",
            ["Ticks"] = _stopwatch.ElapsedTicks
        }, LogLevel.Debug, _category);
    }
}