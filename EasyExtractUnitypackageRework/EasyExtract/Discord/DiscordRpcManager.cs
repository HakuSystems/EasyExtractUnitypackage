using System.Windows;
using DiscordRPC;
using DiscordRPC.Exceptions;
using EasyExtract.Config;

namespace EasyExtract.Discord;

public class DiscordRpcManager : IDisposable
{
    private static DiscordRpcManager? _instance;
    private readonly ConfigHelper _configHelper = new();
    private readonly BetterLogger _logger = new();
    private readonly Timestamps timestamps;
    private DiscordRpcClient client;

    private DiscordRpcManager()
    {
        timestamps = new Timestamps(DateTime.UtcNow);
        DiscordStart();
    }

    public static DiscordRpcManager Instance => _instance ??= new DiscordRpcManager();

    public async void Dispose()
    {
        if (!client.IsDisposed) client.Dispose();
        await _logger.LogAsync("Disposed DiscordRpcClient", "DiscordRpcManager.cs", Importance.Info); // Log disposal
    }

    public async void DiscordStart()
    {
        if (client == null || client.IsDisposed)
        {
            client = new DiscordRpcClient("1103487584124010607");
            client.Initialize(); // Initialize synchronously; no await needed here
            client.OnReady += (sender, e) => Console.WriteLine($"Received Ready from user {e.User.Username}");
            client.OnError += (sender, e) => Console.WriteLine($"Error! {e.Message}");
            client.OnClose += (sender, e) => Console.WriteLine($"Close! {e}");
            await _logger.LogAsync("Discord RPC started", "DiscordRpcManager.cs", Importance.Info); // Log start
        }
    }

    public async Task UpdatePresenceAsync(string state)
    {
        var largeTextString = $"U: {_configHelper.Config.TotalExtracted} F: {_configHelper.Config.TotalFilesExtracted}";
        if (largeTextString.Length > 127)
            try
            {
                largeTextString = largeTextString.Substring(0, 127);
            }
            catch (StringOutOfRangeException e)
            {
                largeTextString = "Too many files extracted and/or unitypackages";
                await _logger.LogAsync($"StringOutOfRangeException: {e.Message}", "DiscordRpcManager.cs",
                    Importance.Warning); // Log exception
            }

        try
        {
            client.SetPresence(new RichPresence
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
            await _logger.LogAsync($"Updated Discord presence to state: {state}", "DiscordRpcManager.cs",
                Importance.Info); // Log presence update
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"Failed to update Discord presence: {ex.Message}", "DiscordRpcManager.cs",
                Importance.Error); // Log presence update failure
            Console.WriteLine($"Failed to Update Discord Presence: {ex.Message}");
        }
    }
}