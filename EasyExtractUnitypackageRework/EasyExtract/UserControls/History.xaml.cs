using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using EasyExtract.Config;
using EasyExtract.Discord;

namespace EasyExtract.UserControls;

public partial class History : UserControl, INotifyPropertyChanged
{
    private ObservableCollection<HistoryModel> _history = new();

    private int _totalExtracted;
    // TotalFilesExtracted & TotalExtracted are not used in this file.

    private int _totalFilesExtracted;

    public History()
    {
        InitializeComponent();
        DataContext = this;
    }


    public int TotalFilesExtracted
    {
        get => _totalFilesExtracted;
        set
        {
            _totalFilesExtracted = value;
            OnPropertyChanged();
        }
    }

    public int TotalExtracted
    {
        get => _totalExtracted;
        set
        {
            _totalExtracted = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<HistoryModel> HistoryList
    {
        get => _history;
        set
        {
            _history = value;
            OnPropertyChanged();
        }
    }

    private ConfigModel? Config { get; set; } = new();

    public event PropertyChangedEventHandler PropertyChanged;

    private async void History_OnLoaded(object sender, RoutedEventArgs e)
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
                await DiscordRpcManager.Instance.UpdatePresenceAsync("History");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }

        TotalExtracted = Config!.TotalExtracted;
        TotalFilesExtracted = Config.TotalFilesExtracted;
        await LoadHistory();
    }

    private async Task LoadHistory()
    {
        Config = await ConfigHelper.LoadConfig();
        if (Config.History == null || Config.History.Count == 0)
        {
            ClearHistoryButton.Visibility = Visibility.Collapsed;
            NoHistoryLabel.Visibility = Visibility.Visible;
            return;
        }
        ClearHistoryButton.Visibility = Visibility.Visible;
        NoHistoryLabel.Visibility = Visibility.Collapsed;
        HistoryList = Config.History;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OpenFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is HistoryModel history)
            Process.Start("explorer.exe", history.ExtractedPath);
    }

    private async void DeleteBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not HistoryModel history) return;
        HistoryList.Remove(history);
        Config!.History.Remove(history);
        await ConfigHelper.UpdateConfig(Config);
        if (HistoryList.Count == 0) NoHistoryLabel.Visibility = Visibility.Visible;
    }

    private async void ClearHistoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        HistoryList.Clear();
        Config!.History.Clear();
        Config.TotalExtracted = 0;
        Config.TotalFilesExtracted = 0;
        TotalExtracted = 0;
        TotalFilesExtracted = 0;
        NoHistoryLabel.Visibility = Visibility.Visible;
        await ConfigHelper.UpdateConfig(Config);
    }
}