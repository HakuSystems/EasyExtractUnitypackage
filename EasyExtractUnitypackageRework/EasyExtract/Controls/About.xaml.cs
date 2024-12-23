using EasyExtract.Config;
using EasyExtract.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;
using Wpf.Ui.Controls;
using Application = System.Windows.Application;

namespace EasyExtract.Controls;

public partial class About
{
    private readonly List<Card> _cards = new();
    private readonly ConfigHelper _configHelper = new();

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
                await DiscordRpcManager.Instance.UpdatePresenceAsync("About");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                await BetterLogger.LogAsync($"Error updating Discord presence: {exception.Message}",
                    Importance.Error); // Log error
                throw;
            }

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
}