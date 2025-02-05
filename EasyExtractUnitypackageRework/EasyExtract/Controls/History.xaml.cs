using System.Collections.ObjectModel;
using System.Windows.Media;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;

namespace EasyExtract.Controls;

public partial class History : UserControl, INotifyPropertyChanged
{
    private ObservableCollection<HistoryModel> _history = new();

    private int _totalExtracted;
    // TotalFilesExtracted & TotalExtracted are not used in this file.

    private int _totalFilesExtracted;

    public History()
    {
        InitializeComponent();
        DataContext = ConfigHandler.Instance.Config;
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
        if (ConfigHandler.Instance.Config.UwUModeActive) BetterUwUifyer.ApplyUwUModeToVisualTree(this);
        await DiscordRpcManager.Instance.TryUpdatePresenceAsync("History");
        TotalExtracted = ConfigHandler.Instance.Config.TotalExtracted;
        TotalFilesExtracted = ConfigHandler.Instance.Config.TotalFilesExtracted;
        await LoadHistory();
        await CalculateTotalExtracted();
        await BetterLogger.LogAsync("History loaded and totals calculated",
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
        ConfigHandler.Instance.Config.TotalExtracted = totalUnitypackagesExtracted;
        ConfigHandler.Instance.Config.TotalFilesExtracted = totalFilesExtracted;

        await BetterLogger.LogAsync("Calculated total extracted files and updated config",
            Importance.Info); // Log calculation
    }

    private async Task LoadHistory()
    {
        if (ConfigHandler.Instance.Config.History == null || ConfigHandler.Instance.Config.History.Count == 0)
        {
            ClearHistoryButton.Visibility = Visibility.Collapsed;
            NoHistoryLabel.Visibility = Visibility.Visible;
            await BetterLogger.LogAsync("No history found", Importance.Info); // Log no history
            return;
        }

        ClearHistoryButton.Visibility = Visibility.Visible;
        NoHistoryLabel.Visibility = Visibility.Collapsed;
        HistoryList = ConfigHandler.Instance.Config.History;
        await BetterLogger.LogAsync("Loaded history", Importance.Info); // Log history load
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
        ConfigHandler.Instance.Config!.History.Remove(history);
        await BetterLogger.LogAsync($"Deleted history item: {history.ExtractedPath}",
            Importance.Info); // Log deletion
        if (HistoryList.Count == 0) NoHistoryLabel.Visibility = Visibility.Visible;
    }

    private async void ClearHistoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        HistoryList.Clear();
        ConfigHandler.Instance.Config!.History.Clear();
        ConfigHandler.Instance.Config.TotalExtracted = 0;
        ConfigHandler.Instance.Config.TotalFilesExtracted = 0;
        TotalExtracted = 0;
        TotalFilesExtracted = 0;
        NoHistoryLabel.Visibility = Visibility.Visible;
        await BetterLogger.LogAsync("Cleared all history", Importance.Info); // Log clearing history
    }


    private void History_OnSizeChanged(object sender, SizeChangedEventArgs e)
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