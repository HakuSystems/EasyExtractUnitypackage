using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace EasyExtract.UserControls;

public partial class About : UserControl
{
    private List<Card> Cards = new();
    public About()
    {
        InitializeComponent();
    }

    private void About_OnLoaded(object sender, RoutedEventArgs e)
    {
        var maxCards = 10;
        for (var i = 0; i < maxCards; i++)
        {
            var card = new Card
            {
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(5),
                Margin= RandomMargin()
            };
            Cards.Add(card);
            RandomCardDesign.Items.Add(card);
        }
        var repeatTimer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(500)};
        repeatTimer.Tick += (o, args) => ChangeRandomMargins();
        repeatTimer.Start();
    }
    
    private Thickness RandomMargin()
    {
        var random = new Random();
        return new Thickness(random.Next(0, 20), random.Next(0), random.Next(0, 20), random.Next(0));
    }
    
    private void ChangeRandomMargins()
    {
        foreach (var card in Cards)
        {
            card.Margin = RandomMargin();
        }
    }

}