using System.Windows;
using System.Windows.Controls;
using EasyExtract.Config;
using EasyExtract.Discord;

namespace EasyExtract.UserControls;

public partial class SearchEverything : UserControl
{
    public SearchEverything()
    {
        InitializeComponent();
    }

    private async void SearchEverything_OnLoaded(object sender, RoutedEventArgs e)
    {
        var isDiscordEnabled = false;
        try
        {
            isDiscordEnabled = (await ConfigHelper.LoadConfig()).DiscordRpc;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }

        if (isDiscordEnabled)
            try
            {
                await DiscordRpcManager.Instance.UpdatePresenceAsync("Searching Everything");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
    }
}