using EasyExtract.Services;

namespace EasyExtract.Controls;

public partial class BetterDetails : UserControl
{
    public BetterDetails()
    {
        InitializeComponent();
    }

    private async void BetterDetails_OnLoaded(object sender, RoutedEventArgs e)
    {
        await DiscordRpcManager.Instance.TryUpdatePresenceAsync("Details");
        // ...
    }
}