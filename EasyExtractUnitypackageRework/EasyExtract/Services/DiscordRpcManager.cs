using DiscordRPC;
using DiscordRPC.Exceptions;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Utilities;
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
            await BetterLogger.LogAsync($"Error updating Discord presence: {ex.Message}", Importance.Error);
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
            await BetterLogger.LogAsync($"Error reading config: {ex.Message}", Importance.Error);
            return false;
        }
    }

    private async Task InitializeAsync()
    {
        if (Client == null || Client.IsDisposed)
        {
            Client = new DiscordRpcClient("1103487584124010607");
            Client.Initialize();
            Client.OnReady += (_, e) => Console.WriteLine($"Received Ready from user {e.User.Username}");
            Client.OnError += (_, e) => Console.WriteLine($"Error! {e.Message}");
            Client.OnClose += (_, e) => Console.WriteLine($"Close! {e}");
            await BetterLogger.LogAsync("Discord RPC started", Importance.Info);
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
                await BetterLogger.LogAsync($"StringOutOfRangeException: {e.Message}", Importance.Warning);
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

        await BetterLogger.LogAsync($"Updated Discord presence to state: {state}", Importance.Info);
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
                BetterLogger.LogAsync("Disposed DiscordRpcClient", Importance.Info).ConfigureAwait(false);
            }

            Client = null!;
            _disposed = true;
        }
    }

    #endregion
}