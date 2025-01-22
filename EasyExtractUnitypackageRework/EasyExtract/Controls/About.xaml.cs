using System.Windows.Media;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;
using Wpf.Ui.Controls;
using Application = System.Windows.Application;

namespace EasyExtract.Controls;

public partial class About
{
    private readonly List<Card> _cards = new();

    public About()
    {
        InitializeComponent();
    }

    private async void About_OnLoaded(object sender, RoutedEventArgs e)
    {
        VersionCard.Footer = $"Version {Application.ResourceAssembly.GetName().Version}";
        await BetterLogger.LogAsync("Set version in About UserControl",
            Importance.Info); // Log version set

        const int maxCards = 10;
        for (var i = 0; i < maxCards; i++)
        {
            var card = new Card
            {
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(5),
                Margin = RandomMargin()
            };
            _cards.Add(card);
        }

        await BetterLogger.LogAsync("Added cards to RandomCardDesign",
            Importance.Info); // Log card addition

        var repeatTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        repeatTimer.Tick += (_, _) => ChangeRandomMargins();
        repeatTimer.Start();

        await DiscordRpcManager.Instance.TryUpdatePresenceAsync("About");

        await BetterLogger.LogAsync("About UserControl loaded", Importance.Info); // Log successful load
    }

    private static Thickness RandomMargin()
    {
        var random = new Random();
        return new Thickness(random.Next(0, 20), random.Next(0), random.Next(0, 20), random.Next(0));
    }

    private void ChangeRandomMargins()
    {
        foreach (var card in _cards) card.Margin = RandomMargin();
    }

    private void About_OnSizeChanged(object sender, SizeChangedEventArgs e)
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
                var scaleFactor = e.NewSize.Width / 1530.0;
                switch (scaleFactor)
                {
                    case < 0.5:
                        scaleFactor = 0.5;
                        break;
                    case > 2.0:
                        scaleFactor = 2.0;
                        break;
                }

                RootBorder.LayoutTransform = new ScaleTransform(scaleFactor, scaleFactor);
                break;
            }
        }
    }
}