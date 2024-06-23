using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EasyExtract.Config;
using EasyExtract.Discord;
using Wpf.Ui.Controls;

namespace EasyExtract.UserControls;

public partial class About : UserControl
{
    private readonly List<Card> _cards = new();
    private readonly BetterLogger _logger = new();
    private readonly ConfigHelper ConfigHelper = new();

    public About()
    {
        InitializeComponent();
    }

    private async void About_OnLoaded(object sender, RoutedEventArgs e)
    {
        VersionCard.Footer = $"Version {Application.ResourceAssembly.GetName().Version}";
        _logger.LogAsync("Set version in About UserControl", "About.xaml.cs", Importance.Info); // Log version set

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
            RandomCardDesign.Items.Add(card);
        }

        _logger.LogAsync("Added cards to RandomCardDesign", "About.xaml.cs", Importance.Info); // Log card addition

        var repeatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        repeatTimer.Tick += (o, args) => ChangeRandomMargins();
        repeatTimer.Start();

        var isDiscordEnabled = false;
        try
        {
            isDiscordEnabled = ConfigHelper.Config.DiscordRpc;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            await _logger.LogAsync($"Error reading config: {exception.Message}", "About.xaml.cs",
                Importance.Error); // Log error
            throw;
        }

        if (isDiscordEnabled)
            try
            {
                await DiscordRpcManager.Instance.UpdatePresenceAsync("About");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                await _logger.LogAsync($"Error updating Discord presence: {exception.Message}", "About.xaml.cs",
                    Importance.Error); // Log error
                throw;
            }

        await _logger.LogAsync("About UserControl loaded", "About.xaml.cs", Importance.Info); // Log successful load
    }

    private Thickness RandomMargin()
    {
        var random = new Random();
        return new Thickness(random.Next(0, 20), random.Next(0), random.Next(0, 20), random.Next(0));
    }

    private void ChangeRandomMargins()
    {
        foreach (var card in _cards) card.Margin = RandomMargin();
    }
}