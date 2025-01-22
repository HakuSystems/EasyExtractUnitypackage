using DiscordRPC;
using DiscordRPC.Exceptions;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Utilities;
using Application = System.Windows.Application;

namespace EasyExtract.Services;

public class DiscordRpcManager : IDisposable
{
    private static DiscordRpcManager? _instance;
    private readonly Timestamps timestamps;
    internal DiscordRpcClient? Client;
    private bool disposedValue;

    private DiscordRpcManager()
    {
        timestamps = new Timestamps(DateTime.UtcNow);
        _ = DiscordStart();
    }

    public static DiscordRpcManager Instance => _instance ??= new DiscordRpcManager();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // Dispose managed state (managed objects)
                if (Client is { IsDisposed: false }) Client.Dispose();
                BetterLogger.LogAsync("Disposed DiscordRpcClient", Importance.Info)
                    .Wait(); // Log disposal
            }

            // Set large fields to null.
            Client = null!;

            disposedValue = true;
        }
    }

    public async Task DiscordStart()
    {
        if (Client == null || Client.IsDisposed)
        {
            Client = new DiscordRpcClient("1103487584124010607");
            Client.Initialize(); // Initialize synchronously; no await needed here
            Client.OnReady += (_, e) => Console.WriteLine($"Received Ready from user {e.User.Username}");
            Client.OnError += (_, e) => Console.WriteLine($"Error! {e.Message}");
            Client.OnClose += (_, e) => Console.WriteLine($"Close! {e}");
            await BetterLogger.LogAsync("Discord RPC started", Importance.Info); // Log start
        }
    }

    public async Task UpdatePresenceAsync(string state)
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
                await BetterLogger.LogAsync($"StringOutOfRangeException: {e.Message}",
                    Importance.Warning); // Log exception
            }

        try
        {
            Client?.SetPresence(new RichPresence
            {
                Details = "A Software to get files out of a .unitypackage",
                State = $"Viewing {state} Page",
                Timestamps = timestamps,
                Assets = new Assets
                {
                    LargeImageKey = "logo",
                    LargeImageText = largeTextString,
                    SmallImageKey = "slogo",
                    SmallImageText = $"V{Application.ResourceAssembly.GetName().Version}"
                }
            });
            await BetterLogger.LogAsync($"Updated Discord presence to state: {state}",
                Importance.Info); // Log presence update
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Failed to update Discord presence: {ex.Message}",
                Importance.Error); // Log presence update failure
            Console.WriteLine($"Failed to Update Discord Presence: {ex.Message}");
        }
    }
}