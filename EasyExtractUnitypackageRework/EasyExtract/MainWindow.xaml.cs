﻿using System.Windows;
using System.Windows.Threading;
using EasyExtract.Discord;

namespace EasyExtract;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void AnimationBehavior_OnLoaded(object sender, RoutedEventArgs e)
    {
        DiscordRpcManager.Instance.DiscordStart();

        var timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(5);
        timer.Tick += (sender, args) =>
        {
            timer.Stop();
            new Dashboard().Show();
            Close();
        };
        timer.Start();
    }
}