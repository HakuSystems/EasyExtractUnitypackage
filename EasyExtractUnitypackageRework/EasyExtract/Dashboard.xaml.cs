using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EasyExtract.Config;
using EasyExtract.Discord;
using EasyExtract.UserControls;
using Wpf.Ui.Controls;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace EasyExtract;

public partial class Dashboard : FluentWindow
{
    private static UserControl ContentFrame;
    private ConfigModel Config { get; set; } = new();
    public Dashboard()
    {
        InitializeComponent();
        DataContext = Config;
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
        NavView.Navigate(typeof(EasterEgg));
    }

    private async void Dashboard_OnLoaded(object sender, RoutedEventArgs e)
    {
        var config = await ConfigHelper.LoadConfig();
        if (config.AutoUpdate)
        {
            //todo: check for update here
        }

        switch (config.IsFirstRun)
        {
            case true:
                NavView.Navigate(typeof(About));
                config.IsFirstRun = false;
                await ConfigHelper.UpdateConfig(config);
                break;
            default:
                NavView.Navigate(ContentFrame.GetType());
                break;
        }

        switch (config.UwUModeActive)
        {
            case true:
                NavView.Opacity = 0.2;
                TitleBar.Title = config.AppTitle;
                Title = config.AppTitle;
                await UwUAnimation();
                break;
            default:
                TitleBar.Title = config.AppTitle;
                Title = config.AppTitle;
                break;
        }
    }

    private async Task UwUAnimation()
    {
        var pastelForeground = new SolidColorBrush(Color.FromArgb(255, 237, 187, 238));
        var textBlock = new TextBlock
        {
            Text = "UwU Mode Activated!",
            FontSize = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = pastelForeground,
            Opacity = 0,
            TextAlignment = TextAlignment.Center,
            RenderTransform = new ScaleTransform(1, 1),
            Margin = new Thickness(0, 0, 0, 50)
        };

        Grid.SetRowSpan(textBlock, int.MaxValue);
        Grid.SetColumnSpan(textBlock, int.MaxValue);
        MainGrid.Children.Add(textBlock);
        var fadeInOutAnimation = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(1)))
        {
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(fadeInOutAnimation);
        Storyboard.SetTarget(fadeInOutAnimation, textBlock);
        Storyboard.SetTargetProperty(fadeInOutAnimation, new PropertyPath("Opacity"));

        textBlock.BeginAnimation(OpacityProperty, fadeInOutAnimation);


        storyboard.Completed += (sender, args) => { MainGrid.Children.Remove(textBlock); };
        storyboard.Begin();
        await Task.Delay(1000);
        NavView.Opacity = 1;
    }
}