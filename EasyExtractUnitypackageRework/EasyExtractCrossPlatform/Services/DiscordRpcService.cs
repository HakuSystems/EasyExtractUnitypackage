using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DiscordRPC;
using DiscordRPC.Logging;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class DiscordRpcService : IDisposable
{
    private const string ClientId = "1103487584124010607";
    private const string LargeImageKey = "logo";
    private const string SmallImageKey = "slogo";
    private const int DiscordStringLimit = 127;
    private const string DetailsText = "A Software to get files out of a .unitypackage";

    private static readonly Lazy<DiscordRpcService> InstanceLazy = new(static () => new DiscordRpcService());
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
}
