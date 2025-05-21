using System.Windows.Media;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;
using EasyExtract.Views;

namespace EasyExtract.Controls;

public partial class EasterEgg
{
    public EasterEgg()
    {
        InitializeComponent();
    }

    private async void EasterEgg_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Dashboard.Instance.NavigateBackBtn.Visibility = Visibility.Visible;

            if (ConfigHandler.Instance.Config.UwUModeActive) BetterUwUifyer.ApplyUwUModeToVisualTree(this);
            await DiscordRpcManager.Instance.TryUpdatePresenceAsync("EasterEgg");
            await BetterLogger.LogAsync("EasterEgg UserControl loaded",
                Importance.Info); // Log successful load
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Error in EasterEgg Loaded: {ex.Message}", Importance.Error);
        }
    }

    private void EasterEgg_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        switch (ConfigHandler.Instance.Config.DynamicScalingMode)
        {
            case DynamicScalingModes.Off:
                break;

            case DynamicScalingModes.Simple:
            {
                break;
            }
            case DynamicScalingModes.Experimental:
            {
                var scaleFactor = e.NewSize.Width / 800.0;

                switch (scaleFactor)
                {
                    case < 0.5:
                        scaleFactor = 0.5;
                        break;
                    case > 2.0:
                        scaleFactor = 2.0;
                        break;
                }

                MainGrid.LayoutTransform = new ScaleTransform(scaleFactor, scaleFactor);
                break;
            }
        }
    }
}