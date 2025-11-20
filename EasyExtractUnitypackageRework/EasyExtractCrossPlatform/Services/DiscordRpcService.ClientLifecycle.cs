using System;
using System.Diagnostics;
using DiscordRPC;
using DiscordRPC.Logging;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class DiscordRpcService
{
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
}
