using EasyExtract.Config;
using EasyExtract.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;

namespace EasyExtract.Controls;

public partial class EasterEgg
{
    private readonly ConfigHelper _configHelper = new();

    public EasterEgg()
    {
        InitializeComponent();
    }

    private async void EasterEgg_OnLoaded(object sender, RoutedEventArgs e)
    {
        bool isDiscordEnabled;
        try
        {
            isDiscordEnabled = _configHelper.Config.DiscordRpc;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            await BetterLogger.LogAsync($"Error reading config: {exception.Message}",
                Importance.Error); // Log error
            throw;
        }

        if (isDiscordEnabled)
            try
            {
                await DiscordRpcManager.Instance.UpdatePresenceAsync("Easter Egg");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                await BetterLogger.LogAsync($"Error updating Discord presence: {exception.Message}",
                    Importance.Error); // Log error
                throw;
            }

        await BetterLogger.LogAsync("EasterEgg UserControl loaded",
            Importance.Info); // Log successful load
    }
}