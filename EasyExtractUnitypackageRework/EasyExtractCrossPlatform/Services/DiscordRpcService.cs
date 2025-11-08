using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRPC.Message;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.Services;

public sealed class DiscordRpcService : IDisposable
{
    private const string ClientId = "1103487584124010607";
    private const string LargeImageKey = "logo";
    private const string SmallImageKey = "slogo";
    private const int DiscordStringLimit = 127;

    private const string DetailsText = "A Software to get files out of a .unitypackage";

    private static readonly Lazy<DiscordRpcService> InstanceLazy = new(static () => new DiscordRpcService());

    private static readonly string LogFilePath =
        Path.Combine(AppSettingsService.SettingsDirectory, "Logs", "discord-rpc.log");

    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim _syncRoot = new(1, 1);
    private DiscordRpcClient? _client;
    private bool _disposed;
    private Timer? _keepAliveTimer;
    private string? _lastPresenceSignature;
    private bool _lastPresenceWasBusy;
    private AppSettings? _lastSettingsSnapshot;
    private DiscordPresenceContext? _pendingContext;
    private Timestamps? _timestamps;

    private DiscordRpcService()
    {
    }

    public static DiscordRpcService Instance => InstanceLazy.Value;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _syncRoot.Wait();
        try
        {
            DisposeClientLocked();
        }
        finally
        {
            _syncRoot.Release();
            _syncRoot.Dispose();
        }
    }

    public async Task UpdatePresenceAsync(AppSettings settings, DiscordPresenceContext context,
        CancellationToken cancellationToken = default)
    {
        if (settings is null)
            throw new ArgumentNullException(nameof(settings));

        if (_disposed)
            return;

        await _syncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Log($"Update requested: state='{context.State}', busy={context.IsBusy}, queue={context.QueueCount}");

            if (!settings.DiscordRpc)
            {
                Log("Discord RPC disabled in settings. Disposing client.");
                DisposeClientLocked(clearPresence: true);
                return;
            }

            _lastSettingsSnapshot = settings;
            _pendingContext = context;
            if (!EnsureClientInitialized())
            {
                Log("Discord RPC client initialization was not completed.");
                return;
            }

            ApplyPresenceLocked(settings, context);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to update Discord Rich Presence: {ex}");
            Log($"UpdatePresenceAsync exception: {ex}");
            LoggingService.LogError("Failed to update Discord Rich Presence.", ex);
            DisposeClientLocked();
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    private bool EnsureClientInitialized()
    {
        if (_client is { IsDisposed: false })
            return true;

        try
        {
            _client = new DiscordRpcClient(ClientId)
            {
                Logger = new DebugLogger(LogLevel.Info),
                SkipIdenticalPresence = false
            };

            AttachClientHandlers(_client);

            _timestamps = new Timestamps(DateTime.UtcNow);
            _lastPresenceSignature = null;
            _lastPresenceWasBusy = false;
            EnsureKeepAliveTimer();
            var initialized = _client.Initialize();
            if (!initialized)
            {
                Debug.WriteLine("[DiscordRPC] Initialize() returned false. Is Discord running?");
                Log("Initialize returned false. Discord might not be running or RPC is disabled.");
                DetachClientHandlers(_client);
                _client.Dispose();
                _client = null;
                LoggingService.LogError("Discord RPC client failed to initialize; Discord may not be running.");
                return false;
            }

            Log("Discord RPC client initialized.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialise Discord Rich Presence: {ex}");
            if (_client is not null)
            {
                DetachClientHandlers(_client);
                _client.Dispose();
            }

            _client = null;
            _timestamps = null;
            _lastPresenceSignature = null;
            Log($"Initialization failed: {ex}");
            LoggingService.LogError("Discord RPC client initialization failed.", ex);
            return false;
        }
    }

    private void DisposeClientLocked(bool clearPresence = false)
    {
        if (_client is { IsDisposed: false })
        {
            DetachClientHandlers(_client);

            if (clearPresence)
            {
                try
                {
                    _client.ClearPresence();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to clear Discord Rich Presence: {ex}");
                    Log($"ClearPresence failed: {ex}");
                }
            }

            _client.Dispose();
        }

        _client = null;
        _timestamps = null;
        _lastPresenceSignature = null;
        _pendingContext = null;
        _lastSettingsSnapshot = null;
        _lastPresenceWasBusy = false;
        _keepAliveTimer?.Dispose();
        _keepAliveTimer = null;
        Log("Discord RPC client disposed.");
    }

    private void AttachClientHandlers(DiscordRpcClient client)
    {
        client.OnReady += OnClientReady;
        client.OnClose += OnClientClosed;
        client.OnError += OnClientError;
        client.OnConnectionFailed += OnClientConnectionFailed;
    }

    private void DetachClientHandlers(DiscordRpcClient client)
    {
        client.OnReady -= OnClientReady;
        client.OnClose -= OnClientClosed;
        client.OnError -= OnClientError;
        client.OnConnectionFailed -= OnClientConnectionFailed;
    }

    private RichPresence BuildPresence(AppSettings settings, DiscordPresenceContext context)
    {
        if (context.IsBusy != _lastPresenceWasBusy)
        {
            _timestamps = new Timestamps(DateTime.UtcNow);
            _lastPresenceWasBusy = context.IsBusy;
        }

        var timestamps = context.IsBusy
            ? _timestamps ??= new Timestamps(DateTime.UtcNow)
            : null;

        return new RichPresence
        {
            Details = TrimToLimit(DetailsText, DiscordStringLimit),
            State = TrimToLimit(BuildStateText(context), DiscordStringLimit),
            Timestamps = timestamps,
            Assets = new Assets
            {
                LargeImageKey = LargeImageKey,
                LargeImageText = BuildLargeImageText(settings, context),
                SmallImageKey = SmallImageKey,
                SmallImageText = BuildSmallImageText(settings, context)
            }
        };
    }

    private static string BuildLargeImageText(AppSettings settings, DiscordPresenceContext context)
    {
        var packages = Math.Max(0, settings.TotalExtracted);
        var files = Math.Max(0, settings.TotalFilesExtracted);
        var baseCounts = $"U: {packages} | F: {files}";

        if (context.IsBusy)
        {
            var current = string.IsNullOrWhiteSpace(context.CurrentPackage)
                ? "Extracting assets"
                : $"Extracting {context.CurrentPackage}";
            var queueSuffix = context.QueueCount > 0 ? $" | {context.QueueCount} left" : string.Empty;
            return TrimToLimit($"{current}{queueSuffix} | {baseCounts}", DiscordStringLimit);
        }

        if (context.QueueCount > 0)
        {
            var nextSuffix = string.IsNullOrWhiteSpace(context.NextPackage)
                ? string.Empty
                : $" | Next: {context.NextPackage}";
            return TrimToLimit($"{baseCounts}{nextSuffix}", DiscordStringLimit);
        }

        return TrimToLimit(baseCounts, DiscordStringLimit);
    }

    private static string BuildSmallImageText(AppSettings settings, DiscordPresenceContext context)
    {
        var version = VersionProvider.GetApplicationVersion();
        var tier = string.IsNullOrWhiteSpace(settings.LicenseTier)
            ? "Free"
            : settings.LicenseTier.Trim();

        var baseText = string.IsNullOrWhiteSpace(version)
            ? "EasyExtract"
            : $"EasyExtract v{version}";

        var queueCount = Math.Max(0, context.QueueCount);
        var queueSuffix = queueCount > 0
            ? $" | {queueCount} in queue"
            : string.Empty;

        var composed = $"{baseText} - {tier} tier{queueSuffix}";
        return TrimToLimit(composed, DiscordStringLimit);
    }

    private static string TrimToLimit(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static string CreatePresenceSignature(RichPresence presence, DiscordPresenceContext context)
    {
        var assets = presence.Assets;
        return
            $"{presence.Details}|{presence.State}|{assets?.LargeImageText}|{assets?.SmallImageText}|{context.QueueCount}|{context.CurrentPackage}|{context.NextPackage}|{context.IsBusy}";
    }

    private void ApplyPresenceLocked(AppSettings settings, DiscordPresenceContext context, bool force = false)
    {
        if (_client is null || _client.IsDisposed)
            return;

        try
        {
            if (!_client.IsInitialized)
                return;

            var presence = BuildPresence(settings, context);
            var signature = CreatePresenceSignature(presence, context);

            if (!force && string.Equals(_lastPresenceSignature, signature, StringComparison.Ordinal))
                return;

            _client.SetPresence(presence);
            _lastPresenceSignature = signature;
            EnsureKeepAliveTimer();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DiscordRPC] Failed to set presence: {ex}");
            Log($"Failed to set presence: {ex}");
        }
    }

    private static string BuildStateText(DiscordPresenceContext context)
    {
        if (context.IsBusy)
        {
            if (!string.IsNullOrWhiteSpace(context.CurrentPackage))
                return $"Extracting {context.CurrentPackage}";

            return "Extracting assets";
        }

        var normalized = string.IsNullOrWhiteSpace(context.State) ? "Dashboard" : context.State.Trim();
        return $"Viewing {normalized} Page";
    }

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

            ApplyPresenceLocked(settings, context.Value, force: true);
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
        Debug.WriteLine($"[DiscordRPC] {message}");

        try
        {
            var timestamp = DateTimeOffset.Now.ToString("u", CultureInfo.InvariantCulture);
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
            File.AppendAllText(LogFilePath, $"[{timestamp}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Swallow logging errors – avoid recursive failures.
        }
    }

    private sealed class DebugLogger : ILogger
    {
        public DebugLogger(LogLevel level)
        {
            Level = level;
        }

        public LogLevel Level { get; set; }

        public void Trace(string message, params object[] args) => Write(LogLevel.Trace, message, args);
        public void Info(string message, params object[] args) => Write(LogLevel.Info, message, args);
        public void Warning(string message, params object[] args) => Write(LogLevel.Warning, message, args);
        public void Error(string message, params object[] args) => Write(LogLevel.Error, message, args);

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

public readonly record struct DiscordPresenceContext(
    string State,
    string Details,
    string? CurrentPackage,
    string? NextPackage,
    int QueueCount,
    bool IsBusy)
{
    public static DiscordPresenceContext Disabled() =>
        new("Rich Presence disabled", "Discord integration disabled by user", null, null, 0, false);
}