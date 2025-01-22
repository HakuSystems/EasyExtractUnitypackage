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
            if (_searchEverythingList == value) return;
            _searchEverythingList = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void SearchEverything_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!await _everythingValidation.AreSystemRequirementsMet())
        {
            FallbackEverything.Visibility = Visibility.Visible;
            FallbackEverything.Text = await _everythingValidation.AreSystemRequirementsMetString();
            await BetterLogger.LogAsync("System requirements not met for Everything", Importance.Warning);
        }
        else
        {
            _allSearchResults.Clear();

            Everything.Everything_SetSearchW("endwith:.unitypackage");
            Everything.Everything_SetRequestFlags(Everything.EVERYTHING_REQUEST_FILE_NAME
                                                  | Everything.EVERYTHING_REQUEST_PATH);
            Everything.Everything_QueryW(true);

            var myThread = new Thread(LoopList)
            {
                IsBackground = true
            };
            myThread.SetApartmentState(ApartmentState.STA);
            myThread.Start();

            FallbackEverything.Visibility = Visibility.Collapsed;
            await BetterLogger.LogAsync("Everything search started", Importance.Info);
        }

        var isDiscordEnabled = false;
        try
        {
            isDiscordEnabled = ConfigHandler.Instance.Config.DiscordRpc;
        }
        catch (Exception exception)
        {
            await BetterLogger.LogAsync($"Error reading config: {exception.Message}", Importance.Error);
            throw;
        }

        if (isDiscordEnabled)
            try
            {
                await DiscordRpcManager.Instance.UpdatePresenceAsync("Search Everything");
            }
            catch (Exception exception)
            {
                await BetterLogger.LogAsync($"Error updating Discord presence: {exception.Message}", Importance.Error);
                throw;
            }
    }

    private async void LoopList()
    {
        var resultCount = Everything.Everything_GetNumResults();
        for (uint i = 0; i < resultCount; i++)
        {
            var path = Marshal.PtrToStringUni(Everything.Everything_GetResultFullPathName(i));
            var name = Marshal.PtrToStringUni(Everything.Everything_GetResultFileName(i));

            if (_allSearchResults.Any(x => x.UnityPackageName == name)) continue;
            if (path != null)
                _allSearchResults.Add(new SearchEverythingModel
                {
                    UnityPackageName = name,
                    UnityPackagePath = path,
                    Id = i,
                    ModifiedTime = "Last Modified Date: " + GetFileDateTime(i, false),
                    CreatedTime = "Creation Date: " + GetFileDateTime(i, true)
                });
        }

        await BetterLogger.LogAsync($"LoopList processed {resultCount} results", Importance.Info);
    }

    private string GetFileDateTime(uint fileIndex, bool isCreationTime)
    {
        var path = Marshal.PtrToStringUni(Everything.Everything_GetResultFullPathName(fileIndex));
        if (path == null) return string.Empty;

        var file = new FileInfo(path);
        return isCreationTime
            ? file.CreationTime.ToString("dd-MM-yyyy")
            : file.LastWriteTime.ToString("dd-MM-yyyy");
    }

    private async void SearchEverythingTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchEverythingTextBox.Text;
        if (string.IsNullOrWhiteSpace(query))
        {
            SearchEverythingList = null;
            FoundText.Text = "Search for a UnityPackage Name";
            await BetterLogger.LogAsync("Search box cleared", Importance.Info);

            // Reset to default (or last known) results when cleared
            SearchEverythingList = _allSearchResults.ToList();
            return;
        }

        // Normalize query
        query = query.Trim();

        // Filter by creation date if toggled on
        if (CreationDateFilterSwitch.IsChecked == true)
        {
            var creationDateStart = CalendarStartCreationDatePicker.Date;
            var endCreationDate = CalendarEndCreationDatePicker.Date;

            SearchEverythingList = _allSearchResults
                .Where(x =>
                {
                    // Convert the "Creation Date: ..." string back to a DateTime
                    var createdDateString = x.CreatedTime.Replace("Creation Date: ", "");
                    var fileCreatedDate = DateTime.Parse(createdDateString);

                    // Partial substring match anywhere in the name (case-insensitive)
                    var containsQuery = x.UnityPackageName
                        .IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0;

                    // Check date filter
                    var isWithinDateRange = fileCreatedDate >= creationDateStart && fileCreatedDate <= endCreationDate;

                    return containsQuery && isWithinDateRange;
                })
                .ToList();
        }
        else
        {
            // No date filter: just partial substring match
            SearchEverythingList = _allSearchResults
                .Where(x => x.UnityPackageName
                    .IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0)
                .ToList();
        }

        FoundText.Text = $"Found {SearchEverythingList.Count} results";
        await BetterLogger.LogAsync($"Search updated, found {SearchEverythingList.Count} results", Importance.Info);
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
            var counter = 0;
            foreach (var file in openFileDialog.FileNames)
            {
                counter++;
                var name = Path.GetFileName(file);
                var duplicate = Extraction.SearchResultQueue?.Find(x => x.UnityPackageName == name);
                if (duplicate != null) continue;

                Extraction.SearchResultQueue ??= new List<SearchEverythingModel>();
                Extraction.SearchResultQueue.Add(new SearchEverythingModel
                {
                    UnityPackageName = name,
                    UnityPackagePath = file,
                    Id = 0,
                    ModifiedTime = string.Empty,
                    CreatedTime = string.Empty
                });

                AddedStatusTxt.Text = counter switch
                {
                    1 => $"Added {name} to the queue",
                    > 1 => $"Added {counter} UnityPackage(s) to the queue",
                    _ => AddedStatusTxt.Text
                };
            }

            await BetterLogger.LogAsync($"Manually added {counter} files to the queue", Importance.Info);
        }
    }

    private async void QueueAddButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SearchEverythingModel selected)
        {
            var name = selected.UnityPackageName;
            var duplicate = Extraction.SearchResultQueue?.Find(x => x.UnityPackageName == name);
            if (duplicate != null) return;

            Extraction.SearchResultQueue ??= new List<SearchEverythingModel>();
            Extraction.SearchResultQueue.Add(new SearchEverythingModel
            {
                UnityPackageName = name,
                UnityPackagePath = selected.UnityPackagePath,
                Id = selected.Id,
                ModifiedTime = string.Empty,
                CreatedTime = string.Empty
            });

            AddedStatusTxt.Text = $"Added {name} to the queue";
            await BetterLogger.LogAsync($"Added {name} to the queue", Importance.Info);
        }
    }

    private void CreationDateFilterSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        CreationDateFilterCard.IsEnabled = false;
        CreationDateFilterCardFallback.Visibility = Visibility.Visible;
        CreationDateFilterCard.Visibility = Visibility.Collapsed;

        var query = SearchEverythingTextBox.Text;
        if (!string.IsNullOrWhiteSpace(query))
        {
            SearchEverythingList = _allSearchResults
                .Where(x => x.UnityPackageName.StartsWith(query, StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            FoundText.Text = $"Found {SearchEverythingList.Count} results";
        }
        else
        {
            // If no query, just reset to default
            SearchEverythingList = _allSearchResults
                .Where(x => x.UnityPackageName.StartsWith(query, StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            FoundText.Text = $"Found {SearchEverythingList.Count} results";
        }
    }

    private void CreationDateFilterSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        CreationDateFilterCard.IsEnabled = true;
        CreationDateFilterCardFallback.Visibility = Visibility.Collapsed;
        CreationDateFilterCard.Visibility = Visibility.Visible;

        var query = SearchEverythingTextBox.Text;
        if (!string.IsNullOrWhiteSpace(query))
        {
            SearchEverythingList = _allSearchResults
                .Where(x => x.UnityPackageName.StartsWith(query, StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            FoundText.Text = $"Found {SearchEverythingList.Count} results";
        }
        else
        {
            // If no search term, apply filter by creation date only
            var creationDateStart = CalendarStartCreationDatePicker.Date;
            var endCreationDate = CalendarEndCreationDatePicker.Date;
            SearchEverythingList = _allSearchResults
                .Where(x =>
                    x.UnityPackageName.StartsWith(query, StringComparison.InvariantCultureIgnoreCase) &&
                    DateTime.Parse(x.CreatedTime.Replace("Creation Date: ", "")) >= creationDateStart &&
                    DateTime.Parse(x.CreatedTime.Replace("Creation Date: ", "")) <= endCreationDate)
                .ToList();
            FoundText.Text = $"Found {SearchEverythingList.Count} results";
        }
    }

    private void UpdateSearchResultCreationDateFilterBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var query = SearchEverythingTextBox.Text;
        var creationDateStart = CalendarStartCreationDatePicker.Date;
        var endCreationDate = CalendarEndCreationDatePicker.Date;

        SearchEverythingList = _allSearchResults
            .Where(x =>
                x.UnityPackageName.StartsWith(query, StringComparison.InvariantCultureIgnoreCase) &&
                DateTime.Parse(x.CreatedTime.Replace("Creation Date: ", "")) >= creationDateStart &&
                DateTime.Parse(x.CreatedTime.Replace("Creation Date: ", "")) <= endCreationDate)
            .ToList();

        FoundText.Text = $"Found {SearchEverythingList.Count} results";
    }

    private void SearchEverything_OnSizeChanged(object sender, SizeChangedEventArgs e)
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

                RootShadowBorder.LayoutTransform = new ScaleTransform(scaleFactor, scaleFactor);
                break;
            }
        }
    }
}