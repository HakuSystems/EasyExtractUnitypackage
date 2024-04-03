using System.Windows;
using DiscordRPC;

namespace EasyExtract.Discord;

public class DiscordRpcManager : IDisposable
{
    private static DiscordRpcManager instance;
    private readonly Timestamps timestamps;
    private DiscordRpcClient client;

    private DiscordRpcManager()
    {
        timestamps = new Timestamps(DateTime.UtcNow);
        DiscordStart();
    }

    public static DiscordRpcManager Instance => instance ??= new DiscordRpcManager();

    public void Dispose()
    {
        if (!client.IsDisposed) client.Dispose();
    }

    public void DiscordStart()
    {
        if (client == null || client.IsDisposed)
        {
            client = new DiscordRpcClient("1103487584124010607");
            client.Initialize(); // Initialize synchronously; no await needed here
            client.OnReady += (sender, e) => Console.WriteLine($"Received Ready from user {e.User.Username}");
            client.OnError += (sender, e) => Console.WriteLine($"Error! {e.Message}");
            client.OnClose += (sender, e) => Console.WriteLine($"Close! {e}");
        }
    }

    public async Task UpdatePresenceAsync(string state)
    {
        try
        {
            client.SetPresence(new RichPresence
            {
                Details = "A Software to get files out of a .unitypackage",
                State = state,
                Timestamps = timestamps,
                Assets = new Assets
                {
                    LargeImageKey = "logo",
                    LargeImageText = "EasyExtract",
                    SmallImageKey = "slogo",
                    SmallImageText = $"V{Application.ResourceAssembly.GetName().Version}"
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to Update Discord Presence: {ex.Message}");
        }
    }
}