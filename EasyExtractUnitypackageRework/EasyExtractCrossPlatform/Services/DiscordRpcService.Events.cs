using DiscordRPC.Logging;
using DiscordRPC.Message;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class DiscordRpcService
{
    private void OnClientReady(object? sender, ReadyMessage message)
    {
        Debug.WriteLine($"[DiscordRPC] Ready as {message.User?.Username ?? "unknown user"}");
        Log($"OnReady: user='{message.User?.Username ?? "unknown"}'");

        var settings = _lastSettingsSnapshot;
        var context = _pendingContext;
        if (settings is null || context is null)
            return;

        _ = Task.Run(async () =>
        {
            await _syncRoot.WaitAsync().ConfigureAwait(false);
            try
            {
                ApplyPresenceLocked(settings, context.Value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DiscordRPC] Failed to apply pending presence on ready: {ex}");
            }
            finally
            {
                _syncRoot.Release();
            }
        });
    }

    private void OnClientClosed(object? sender, CloseMessage message)
    {
        Debug.WriteLine($"[DiscordRPC] Connection closed: {message.Reason} ({message.Code})");
        Log($"OnClose: reason='{message.Reason}', code={message.Code}");
    }

    private void OnClientError(object? sender, ErrorMessage message)
    {
        Debug.WriteLine($"[DiscordRPC] Error {message.Code}: {message.Message}");
        Log($"OnError: code={message.Code}, message='{message.Message}'");
    }

    private void OnClientConnectionFailed(object? sender, ConnectionFailedMessage message)
    {
        Debug.WriteLine($"[DiscordRPC] Connection failed: {message.Type} on pipe {message.FailedPipe}");
        Log($"OnConnectionFailed: type={message.Type}, pipe={message.FailedPipe}");
    }

    private void EnsureKeepAliveTimer()
    {
        if (_keepAliveTimer is null)
        {
            _keepAliveTimer = new Timer(_ => KeepAliveCallback(), null, KeepAliveInterval, Timeout.InfiniteTimeSpan);
            return;
        }

        _keepAliveTimer.Change(KeepAliveInterval, Timeout.InfiniteTimeSpan);
    }

    private void KeepAliveCallback()
    {
        if (_disposed)
            return;

        var settings = _lastSettingsSnapshot;
        var context = _pendingContext;
        if (settings is null || context is null)
        {
            _keepAliveTimer?.Change(KeepAliveInterval, Timeout.InfiniteTimeSpan);
            return;
        }

        _syncRoot.Wait();
        try
        {
            if (_client is null || _client.IsDisposed || !_client.IsInitialized)
                return;

            ApplyPresenceLocked(settings, context.Value, true);
        }
        catch (Exception ex)
        {
            Log($"KeepAliveCallback failed: {ex}");
        }
        finally
        {
            _syncRoot.Release();
            _keepAliveTimer?.Change(KeepAliveInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private static void Log(string message)
    {
        var formatted = $"[DiscordRPC] {message}";
        Debug.WriteLine(formatted);
        LoggingService.LogInformation(formatted);
    }

    private sealed class DebugLogger : ILogger
    {
        public DebugLogger(LogLevel level)
        {
            Level = level;
        }

        public LogLevel Level { get; set; }

        public void Trace(string message, params object[] args)
        {
            Write(LogLevel.Trace, message, args);
        }

        public void Info(string message, params object[] args)
        {
            Write(LogLevel.Info, message, args);
        }

        public void Warning(string message, params object[] args)
        {
            Write(LogLevel.Warning, message, args);
        }

        public void Error(string message, params object[] args)
        {
            Write(LogLevel.Error, message, args);
        }

        private void Write(LogLevel level, string message, params object[] args)
        {
            if (level < Level)
                return;

            var formatted = args is { Length: > 0 }
                ? string.Format(CultureInfo.InvariantCulture, message, args)
                : message;
            Log(formatted);
            Debug.WriteLine($"[DiscordRPC] {level}: {formatted}");
        }
    }
}