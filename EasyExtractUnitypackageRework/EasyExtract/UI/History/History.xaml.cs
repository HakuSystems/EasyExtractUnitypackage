using System.Collections.ObjectModel;
using EasyExtract.Config;
using EasyExtract.Services.Discord;
using EasyExtract.Utilities;

namespace EasyExtract.UI.History;

public partial class History : UserControl, INotifyPropertyChanged
{
    private readonly BetterLogger _logger = new();
    private readonly ConfigHelper ConfigHelper = new();
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


    public event PropertyChangedEventHandler PropertyChanged;

    private async void History_OnLoaded(object sender, RoutedEventArgs e)
    {
        var isDiscordEnabled = false;
        try
        {
            isDiscordEnabled = ConfigHelper.Config.DiscordRpc;
        }
        catch (Exception exception)
        {
            await _logger.LogAsync($"Error reading config: {exception.Message}", "History.xaml.cs",
                Importance.Error); // Log error
            throw;
        }

        if (isDiscordEnabled)
            try
            {
                await DiscordRpcManager.Instance.UpdatePresenceAsync("History");
            }
            catch (Exception exception)
            {
                await _logger.LogAsync($"Error updating Discord presence: {exception.Message}", "History.xaml.cs",
                    Importance.Error); // Log error
                throw;
            }

        TotalExtracted = ConfigHelper.Config.TotalExtracted;
        TotalFilesExtracted = ConfigHelper.Config.TotalFilesExtracted;
        await LoadHistory();
        await CalculateTotalExtracted();
        await _logger.LogAsync("History loaded and totals calculated", "History.xaml.cs",
            Importance.Info); // Log completion
    }

    private async Task CalculateTotalExtracted()
    {
        // calculate total extracted files in each history item
        // and update the total extracted files count in the config
        var totalFilesExtracted = 0;
        var totalUnitypackagesExtracted = 0;

        foreach (var history in HistoryList)
        {
            totalFilesExtracted += history.TotalFiles;
            totalUnitypackagesExtracted++;
        }

        TotalFilesExtracted = totalFilesExtracted;
        TotalExtracted = totalUnitypackagesExtracted;
        ConfigHelper.Config.TotalExtracted = totalUnitypackagesExtracted;
        ConfigHelper.Config.TotalFilesExtracted = totalFilesExtracted;

        await ConfigHelper.UpdateConfigAsync();
        await _logger.LogAsync("Calculated total extracted files and updated config", "History.xaml.cs",
            Importance.Info); // Log calculation
    }

    private async Task LoadHistory()
    {
        if (ConfigHelper.Config.History == null || ConfigHelper.Config.History.Count == 0)
        {
            ClearHistoryButton.Visibility = Visibility.Collapsed;
            NoHistoryLabel.Visibility = Visibility.Visible;
            await _logger.LogAsync("No history found", "History.xaml.cs", Importance.Info); // Log no history
            return;
        }

        ClearHistoryButton.Visibility = Visibility.Visible;
        NoHistoryLabel.Visibility = Visibility.Collapsed;
        HistoryList = ConfigHelper.Config.History;
        await _logger.LogAsync("Loaded history", "History.xaml.cs", Importance.Info); // Log history load
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
        ConfigHelper.Config!.History.Remove(history);
        await ConfigHelper.UpdateConfigAsync();
        await _logger.LogAsync($"Deleted history item: {history.ExtractedPath}", "History.xaml.cs",
            Importance.Info); // Log deletion
        if (HistoryList.Count == 0) NoHistoryLabel.Visibility = Visibility.Visible;
    }

    private async void ClearHistoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        HistoryList.Clear();
        ConfigHelper.Config!.History.Clear();
        ConfigHelper.Config.TotalExtracted = 0;
        ConfigHelper.Config.TotalFilesExtracted = 0;
        TotalExtracted = 0;
        TotalFilesExtracted = 0;
        NoHistoryLabel.Visibility = Visibility.Visible;
        await ConfigHelper.UpdateConfigAsync();
        await _logger.LogAsync("Cleared all history", "History.xaml.cs", Importance.Info); // Log clearing history
    }
}