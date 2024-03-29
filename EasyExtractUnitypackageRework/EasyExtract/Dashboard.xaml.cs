using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EasyExtract.UserControls;
using Wpf.Ui.Controls;

namespace EasyExtract;

public partial class Dashboard : FluentWindow
{
    private UserControl ContentFrame;
    public Dashboard()
    {
        InitializeComponent();
        DataContext = this;
        ContentFrame = new Extraction();
    }

    private void HeartIcon_OnMouseEnter(object sender, MouseEventArgs e)
    {
        HeartIcon.Symbol = SymbolRegular.HeartBroken24;
        HeartIcon.Foreground = new SolidColorBrush(Colors.Red);
    }

    private void HeartIcon_OnMouseLeave(object sender, MouseEventArgs e)
    {
        HeartIcon.Symbol = SymbolRegular.Heart24;
        HeartIcon.Foreground = new SolidColorBrush(Colors.White);
    }

    private void HeartIcon_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        HeartIcon.Symbol = SymbolRegular.HeartPulse24;
        HeartIcon.Foreground = new SolidColorBrush(Colors.Red);
        NavView.Navigate(typeof(EasterEgg));
    }

    private void Dashboard_OnLoaded(object sender, RoutedEventArgs e)
    {
        NavView.Navigate("Extraction");
    }
}