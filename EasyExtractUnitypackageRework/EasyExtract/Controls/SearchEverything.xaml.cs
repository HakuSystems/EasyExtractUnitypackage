using System.Windows.Media;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;

namespace EasyExtract.Controls;

public partial class SearchEverything : UserControl, INotifyPropertyChanged
{
    private readonly List<SearchEverythingModel> _allSearchResults = new();
    private readonly EverythingValidation _everythingValidation = new();
    private List<SearchEverythingModel>? _searchEverythingList;

    public SearchEverything()
    {
        InitializeComponent();
        DataContext = this;
    }

    public List<SearchEverythingModel>? SearchEverythingList
    {
        get => _searchEverythingList;
        set
        {
            if (_searchEverythingList != value)
            {
                _searchEverythingList = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void SearchEverything_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ConfigHandler.Instance.Config.UwUModeActive)
            BetterUwUifyer.ApplyUwUModeToVisualTree(this);

        if (!await _everythingValidation.AreSystemRequirementsMetAsync())
        {
            FallbackEverything.Visibility = Visibility.Visible;
            FallbackEverything.Text = await _everythingValidation.GetSystemRequirementsStatusAsync();
            await BetterLogger.LogAsync("System requirements not met for Everything", Importance.Warning);
            return;
        }

        FallbackEverything.Visibility = Visibility.Collapsed;
        _allSearchResults.Clear();

        // Setup search parameters
        Everything.Everything_SetSearchW("endwith:.unitypackage");
        Everything.Everything_SetRequestFlags(Everything.RequestFileName | Everything.RequestPath);
        Everything.Everything_QueryW(true);

        // Offload the population work to a background task
        await Task.Run(() => PopulateSearchResults());

        await BetterLogger.LogAsync("Everything search completed", Importance.Info);
        await DiscordRpcManager.Instance.TryUpdatePresenceAsync("Search Everything");
    }

    private void PopulateSearchResults()
    {
        var resultCount = Everything.Everything_GetNumResults();
        for (uint i = 0; i < resultCount; i++)
        {
            var path = Everything.GetResultFullPathName(i);
            var name = Marshal.PtrToStringUni(Everything.Everything_GetResultFileName(i));

            if (string.IsNullOrWhiteSpace(name)) continue;
            if (_allSearchResults.Any(x =>
                    x.UnityPackageName.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
                continue;

            if (!string.IsNullOrWhiteSpace(path))
            {
                var model = new SearchEverythingModel
                {
                    UnityPackageName = name,
                    UnityPackagePath = path,
                    Id = i,
                    ModifiedTime = "Last Modified Date: " + GetFileDateTime(i, false),
                    CreatedTime = "Creation Date: " + GetFileDateTime(i, true)
                };
                _allSearchResults.Add(model);
            }
        }

        // Update the bound list on the UI thread
        Dispatcher.Invoke(() => SearchEverythingList = _allSearchResults.ToList());
        BetterLogger.LogAsync($"PopulateSearchResults processed {resultCount} results", Importance.Info).Wait();
    }

    private string GetFileDateTime(uint fileIndex, bool isCreationTime)
    {
        var path = Everything.GetResultFullPathName(fileIndex);
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        var fileInfo = new FileInfo(path);
        return isCreationTime
            ? fileInfo.CreationTime.ToString("dd-MM-yyyy")
            : fileInfo.LastWriteTime.ToString("dd-MM-yyyy");
    }

    private async void SearchEverythingTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchEverythingTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            SearchEverythingList = _allSearchResults.ToList();
            FoundText.Text = "Search for a UnityPackage Name";
            await BetterLogger.LogAsync("Search box cleared", Importance.Info);
            return;
        }

        FilterResults();
        await BetterLogger.LogAsync($"Search updated, found {SearchEverythingList?.Count ?? 0} results",
            Importance.Info);
    }

    private async void SearchFileManuallyButton_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Unity Package|*.unitypackage",
            Title = "Select a Unity Package",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            var addedCount = 0;
            foreach (var file in openFileDialog.FileNames)
            {
                var name = Path.GetFileName(file);
                if (Extraction.SearchResultQueue?.Any(x =>
                        x.UnityPackageName.Equals(name, StringComparison.InvariantCultureIgnoreCase)) == true)
                    continue;

                Extraction.SearchResultQueue ??= new List<SearchEverythingModel>();
                Extraction.SearchResultQueue.Add(new SearchEverythingModel
                {
                    UnityPackageName = name,
                    UnityPackagePath = file,
                    Id = 0,
                    ModifiedTime = string.Empty,
                    CreatedTime = string.Empty
                });
                addedCount++;
                AddedStatusTxt.Text = addedCount == 1
                    ? $"Added {name} to the queue"
                    : $"Added {addedCount} UnityPackage(s) to the queue";
            }

            await BetterLogger.LogAsync($"Manually added {addedCount} files to the queue", Importance.Info);
        }
    }

    private async void QueueAddButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SearchEverythingModel selected)
        {
            if (Extraction.SearchResultQueue?.Any(x =>
                    x.UnityPackageName.Equals(selected.UnityPackageName,
                        StringComparison.InvariantCultureIgnoreCase)) == true)
                return;

            Extraction.SearchResultQueue ??= new List<SearchEverythingModel>();
            Extraction.SearchResultQueue.Add(new SearchEverythingModel
            {
                UnityPackageName = selected.UnityPackageName,
                UnityPackagePath = selected.UnityPackagePath,
                Id = selected.Id,
                ModifiedTime = string.Empty,
                CreatedTime = string.Empty
            });
            AddedStatusTxt.Text = $"Added {selected.UnityPackageName} to the queue";
            await BetterLogger.LogAsync($"Added {selected.UnityPackageName} to the queue", Importance.Info);
        }
    }

    private void CreationDateFilterSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        CreationDateFilterCard.IsEnabled = false;
        CreationDateFilterCardFallback.Visibility = Visibility.Visible;
        CreationDateFilterCard.Visibility = Visibility.Collapsed;
        FilterResults();
    }

    private void CreationDateFilterSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        CreationDateFilterCard.IsEnabled = true;
        CreationDateFilterCardFallback.Visibility = Visibility.Collapsed;
        CreationDateFilterCard.Visibility = Visibility.Visible;
        FilterResults();
    }

    private void UpdateSearchResultCreationDateFilterBtn_OnClick(object sender, RoutedEventArgs e)
    {
        FilterResults();
    }

    /// <summary>
    ///     Applies search term and, if enabled, date range filters to the result list.
    /// </summary>
    private void FilterResults()
    {
        var query = SearchEverythingTextBox.Text.Trim();
        var creationDateStart = (DateTime)CalendarStartCreationDatePicker.Date;
        var endCreationDate = (DateTime)CalendarEndCreationDatePicker.Date;

        if (CreationDateFilterSwitch.IsChecked == true)
            SearchEverythingList = _allSearchResults.Where(x =>
            {
                var matchesQuery = x.UnityPackageName.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0;
                var fileCreatedDate = DateTime.Parse(x.CreatedTime.Replace("Creation Date: ", ""));
                var withinRange = fileCreatedDate >= creationDateStart && fileCreatedDate <= endCreationDate;
                return matchesQuery && withinRange;
            }).ToList();
        else
            SearchEverythingList = _allSearchResults.Where(x =>
                x.UnityPackageName.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
        FoundText.Text = $"Found {SearchEverythingList?.Count ?? 0} results";
    }

    private void SearchEverything_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        switch (ConfigHandler.Instance.Config.DynamicScalingMode)
        {
            case DynamicScalingModes.Off:
                break;
            case DynamicScalingModes.Simple:
                // (Implement simple scaling if needed.)
                break;
            case DynamicScalingModes.Experimental:
                var scaleFactor = Math.Max(0.5, Math.Min(2.0, e.NewSize.Width / 800.0));
                MainGrid.LayoutTransform = new ScaleTransform(scaleFactor, scaleFactor);
                break;
        }
    }
}