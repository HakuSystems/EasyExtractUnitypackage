using System.Windows;
using System.Windows.Controls;
using EasyExtract.Config;
using EasyExtract.Discord;

namespace EasyExtract.UserControls;

public partial class Extraction : UserControl
{
    public Extraction()
    {
        InitializeComponent();
    }

    private async void Extraction_OnLoaded(object sender, RoutedEventArgs e)
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
                await DiscordRpcManager.Instance.UpdatePresenceAsync("Extraction");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
    }
}