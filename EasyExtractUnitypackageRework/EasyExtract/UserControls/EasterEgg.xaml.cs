using System.Windows;
using System.Windows.Controls;
using EasyExtract.Config;
using EasyExtract.Discord;

namespace EasyExtract.UserControls;

public partial class EasterEgg : UserControl
{
    private readonly BetterLogger _logger = new();
    private readonly ConfigHelper ConfigHelper = new();

    public EasterEgg()
    {
        InitializeComponent();
    }

    private async void EasterEgg_OnLoaded(object sender, RoutedEventArgs e)
    {
        var isDiscordEnabled = false;
        try
        {
            isDiscordEnabled = (await ConfigHelper.ReadConfigAsync())!.DiscordRpc;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            await _logger.LogAsync($"Error reading config: {exception.Message}", "EasterEgg.xaml.cs",
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
                await _logger.LogAsync($"Error updating Discord presence: {exception.Message}", "EasterEgg.xaml.cs",
                    Importance.Error); // Log error
                throw;
            }

        await _logger.LogAsync("EasterEgg UserControl loaded", "EasterEgg.xaml.cs",
            Importance.Info); // Log successful load
    }
}