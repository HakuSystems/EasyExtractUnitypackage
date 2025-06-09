using DiscordRPC;
using DiscordRPC.Exceptions;
using EasyExtract.Config;
using EasyExtract.Utilities.Logger;
using Application = System.Windows.Application;

namespace EasyExtract.Services;

public sealed class DiscordRpcManager : IDisposable
{
    private static DiscordRpcManager? _instance;
    private readonly Timestamps _timestamps;
    private bool _disposed;
    internal DiscordRpcClient? Client;

    private DiscordRpcManager()
    {
        _timestamps = new Timestamps(DateTime.UtcNow);
        _ = InitializeAsync();
    }

    public static DiscordRpcManager Instance => _instance ??= new DiscordRpcManager();

    public async Task TryUpdatePresenceAsync(string state)
    {
        if (!await IsDiscordEnabled())
            return;

        await InitializeAsync();

        try
        {
            await UpdatePresenceInternalAsync(state);
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Error updating Discord presence", "Discord");
        }
    }

    private static async Task<bool> IsDiscordEnabled()
    {
        try
        {
            return ConfigHandler.Instance.Config.DiscordRpc;
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Error reading config", "Discord");
            return false;
        }
    }

    private async Task InitializeAsync()
    {
        if (Client == null || Client.IsDisposed)
        {
            Client = new DiscordRpcClient("1103487584124010607");
            Client.Initialize();
            Client.OnReady += (_, e) => BetterLogger.LogWithContext("Discord RPC Ready",
                new Dictionary<string, object> { { "Username", e.User.Username } }, LogLevel.Info, "Discord");
            Client.OnError += (_, e) => BetterLogger.LogWithContext("Discord RPC Error",
                new Dictionary<string, object> { { "Error", e.Message } }, LogLevel.Error, "Discord");
            Client.OnClose += (_, e) => BetterLogger.LogWithContext("Discord RPC Closed",
                new Dictionary<string, object> { { "Close", e.ToString() } }, LogLevel.Info, "Discord");
            BetterLogger.LogWithContext("Discord RPC started",
                new Dictionary<string, object> { { "ClientId", "1103487584124010607" } }, LogLevel.Info, "Discord");
        }
    }

    private async Task UpdatePresenceInternalAsync(string state)
    {
        var unityPackageCount = ConfigHandler.Instance.Config.TotalExtracted;
        var fileCount = ConfigHandler.Instance.Config.TotalFilesExtracted;
        var largeTextString = $"U: {unityPackageCount} | F: {fileCount}";

        if (largeTextString.Length > 127)
            try
            {
                largeTextString = largeTextString.Substring(0, 127);
            }
            catch (StringOutOfRangeException e)
            {
                largeTextString = "Too many files extracted and/or unity packages";
                BetterLogger.Exception(e, "String length exceeded maximum", "Discord");
            }

        Client?.SetPresence(new RichPresence
        {
            Details = "A Software to get files out of a .unitypackage",
            State = $"Viewing {state} Page",
            Timestamps = _timestamps,
            Assets = new Assets
            {
                LargeImageKey = "logo",
                LargeImageText = largeTextString,
                SmallImageKey = "slogo",
                SmallImageText = $"V{Application.ResourceAssembly.GetName().Version}"
            }
        });

        BetterLogger.LogWithContext("Updated Discord presence",
            new Dictionary<string, object> { { "State", state }, { "Stats", largeTextString } }, LogLevel.Info,
            "Discord");
    }

    #region IDisposable Implementation

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && Client is { IsDisposed: false })
            {
                Client.Dispose();
                BetterLogger.LogWithContext("Disposed Discord RPC Client", new Dictionary<string, object>(),
                    LogLevel.Info, "Discord");
            }

            Client = null!;
            _disposed = true;
        }
    }

    #endregion
}