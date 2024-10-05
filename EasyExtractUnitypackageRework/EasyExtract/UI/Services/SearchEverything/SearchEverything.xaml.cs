using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using EasyExtract.Config;
using EasyExtract.Discord;
using EasyExtract.SearchEverything;
using Microsoft.Win32;

namespace EasyExtract.UserControls;

public partial class SearchEverything : UserControl, INotifyPropertyChanged
{
    private readonly BetterLogger _logger = new();
    private readonly List<SearchEverythingModel> _tempList = new();
    private readonly ConfigHelper ConfigHelper = new();
    private readonly EverythingValidation EverythingValidation = new();

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
        if (!await EverythingValidation.AreSystemRequirementsMet())
        {
            SearchEverythingCard.Visibility = Visibility.Collapsed;
            FallbackEverything.Visibility = Visibility.Visible;
            FallbackEverything.Text = await EverythingValidation.AreSystemRequirementsMetString();
            await _logger.LogAsync("System requirements not met for Everything", "SearchEverything.xaml.cs",
                Importance.Warning); // Log system requirements not met
        }
        else
        {
            _tempList.Clear();

            Everything.Everything_SetSearchW("endwith:.unitypackage");
            Everything.Everything_SetRequestFlags(Everything.EVERYTHING_REQUEST_FILE_NAME
                                                  | Everything.EVERYTHING_REQUEST_PATH
            );
            Everything.Everything_QueryW(true);

            var myThread = new Thread(() => { LoopList(); });
            myThread.SetApartmentState(ApartmentState.STA);
            myThread.Start();

            SearchEverythingCard.Visibility = Visibility.Visible;
            FallbackEverything.Visibility = Visibility.Collapsed;
            await _logger.LogAsync("Everything search started", "SearchEverything.xaml.cs",
                Importance.Info); // Log search start
        }

        var isDiscordEnabled = false;
        try
        {
            isDiscordEnabled = ConfigHelper.Config.DiscordRpc;
        }
        catch (Exception exception)
        {
            await _logger.LogAsync($"Error reading config: {exception.Message}", "SearchEverything.xaml.cs",
                Importance.Error); // Log error
            throw;
        }

        if (isDiscordEnabled)
            try
            {
                await DiscordRpcManager.Instance.UpdatePresenceAsync("Search Everything");
            }
            catch (Exception exception)
            {
                await _logger.LogAsync($"Error updating Discord presence: {exception.Message}",
                    "SearchEverything.xaml.cs", Importance.Error); // Log error
                throw;
            }
    }

    private async void LoopList()
    {
        uint i;

        for (i = 0; i < Everything.Everything_GetNumResults(); i++)
        {
            var path = Marshal.PtrToStringUni(Everything.Everything_GetResultFullPathName(i));
            var name = Marshal.PtrToStringUni(Everything.Everything_GetResultFileName(i));
            var duplicate = _tempList.Find(x => x.UnityPackageName == name);
            if (duplicate != null) continue;
            if (path != null)
                _tempList.Add(new SearchEverythingModel
                {
                    UnityPackageName = name,
                    UnityPackagePath = path,
                    Id = i,
                    ModifiedTime = "Last Modified Date: " + GetFileDateTime(i, false),
                    CreatedTime = "Creation Date: " + GetFileDateTime(i, true)
                });
        }

        await _logger.LogAsync($"LoopList processed {i} results", "SearchEverything.xaml.cs",
            Importance.Info); // Log loop processing
    }

    /// <summary>
    ///     Retrieves the created or modified time of a file given its index in the search results.
    /// </summary>
    /// <param name="fileIndex">The index of the file in the search results.</param>
    /// <param name="isCreationTime">
    ///     Specifies whether to retrieve the creation time (true) or the last modified time (false)
    ///     of the file.
    /// </param>
    /// <returns>
    ///     The created or modified time of the file in the format "dd-MM-yyyy HH:mm:ss".
    /// </returns>
    private string GetFileDateTime(uint fileIndex, bool isCreationTime)
    {
        var path = Marshal.PtrToStringUni(Everything.Everything_GetResultFullPathName(fileIndex));
        var file = new FileInfo(path);
        return isCreationTime
            ? file.CreationTime.ToString("dd-MM-yyyy")
            : file.LastWriteTime.ToString("dd-MM-yyyy");
    }

    private async void SearchEverythingTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(SearchEverythingTextBox.Text))
        {
            SearchEverythingList = null;
            FoundText.Text = "Search for a UnityPackage Name";
            await _logger.LogAsync("Search box cleared", "SearchEverything.xaml.cs",
                Importance.Info); // Log search clear
            return;
        }

        if (CreationDateFilterSwitch.IsChecked == true)
        {
            var creationDateStart = CalendarStartCreationDatePicker.Date;
            var endCreationDate = CalendarEndCreationDatePicker.Date;
            SearchEverythingList = _tempList.Where(x =>
                    x.UnityPackageName.StartsWith(SearchEverythingTextBox.Text,
                        StringComparison.InvariantCultureIgnoreCase) &&
                    DateTime.Parse(x.CreatedTime.Replace("Creation Date: ", "")) >= creationDateStart &&
                    DateTime.Parse(x.CreatedTime.Replace("Creation Date: ", "")) <= endCreationDate)
                .ToList();
            FoundText.Text = $"Found {SearchEverythingList.Count} results";
        }
        else
        {
            SearchEverythingList = _tempList.Where(x =>
                    x.UnityPackageName.StartsWith(SearchEverythingTextBox.Text,
                        StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            FoundText.Text = $"Found {SearchEverythingList.Count} results";
        }


        await _logger.LogAsync($"Search updated, found {SearchEverythingList.Count} results",
            "SearchEverything.xaml.cs", Importance.Info); // Log search update
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
                var duplicate = Extraction._queueList?.Find(x => x.UnityPackageName == name);
                if (duplicate != null) continue;
                if (Extraction._queueList == null) Extraction._queueList = new List<SearchEverythingModel>();
                Extraction._queueList.Add(new SearchEverythingModel
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

            await _logger.LogAsync($"Manually added {counter} files to the queue", "SearchEverything.xaml.cs",
                Importance.Info); // Log manual addition
        }
    }

    private async void QueueAddButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = (SearchEverythingModel)((Button)sender).DataContext;
        var id = selected.Id;
        var name = selected.UnityPackageName;
        var path = selected.UnityPackagePath;

        var duplicate = Extraction._queueList?.Find(x => x.UnityPackageName == name);
        if (duplicate != null) return;
        if (Extraction._queueList == null) Extraction._queueList = new List<SearchEverythingModel>();
        Extraction._queueList.Add(new SearchEverythingModel
        {
            UnityPackageName = name,
            UnityPackagePath = path,
            Id = id,
            ModifiedTime = string.Empty,
            CreatedTime = string.Empty
        });
        AddedStatusTxt.Text = $"Added {name} to the queue";

        await _logger.LogAsync($"Added {name} to the queue", "SearchEverything.xaml.cs",
            Importance.Info); // Log queue addition
    }

    private void CreationDateFilterSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        CreationDateFilterCard.IsEnabled = false;
        CreationDateFilterCardFallback.Visibility = Visibility.Visible;
        CreationDateFilterCard.Visibility = Visibility.Collapsed;
        if (!string.IsNullOrEmpty(SearchEverythingTextBox.Text))
        {
            SearchEverythingList = _tempList.Where(x =>
                    x.UnityPackageName.StartsWith(SearchEverythingTextBox.Text,
                        StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            FoundText.Text = $"Found {SearchEverythingList.Count} results";
        }
        else
        {
            SearchEverythingList = _tempList.Where(x =>
                {
                    return x.UnityPackageName.StartsWith(SearchEverythingTextBox.Text,
                        StringComparison.InvariantCultureIgnoreCase);
                })
                .ToList();
            FoundText.Text = $"Found {SearchEverythingList.Count} results";
        }
    }

    private void CreationDateFilterSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        CreationDateFilterCard.IsEnabled = true;
        CreationDateFilterCardFallback.Visibility = Visibility.Collapsed;
        CreationDateFilterCard.Visibility = Visibility.Visible;
        if (!string.IsNullOrEmpty(SearchEverythingTextBox.Text))
        {
            SearchEverythingList = _tempList.Where(x =>
                    x.UnityPackageName.StartsWith(SearchEverythingTextBox.Text,
                        StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            FoundText.Text = $"Found {SearchEverythingList.Count} results";
        }
        else
        {
            var creationDateStart = CalendarStartCreationDatePicker.Date;
            var endCreationDate = CalendarEndCreationDatePicker.Date;
            SearchEverythingList = _tempList.Where(x =>
                    x.UnityPackageName.StartsWith(SearchEverythingTextBox.Text,
                        StringComparison.InvariantCultureIgnoreCase) &&
                    DateTime.Parse(x.CreatedTime.Replace("Creation Date: ", "")) >= creationDateStart &&
                    DateTime.Parse(x.CreatedTime.Replace("Creation Date: ", "")) <= endCreationDate)
                .ToList();
            FoundText.Text = $"Found {SearchEverythingList.Count} results";
        }
    }

    private void UpdateSearchResultCreationDateFilterBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var textinput = SearchEverythingTextBox.Text;
        var creationDateStart = CalendarStartCreationDatePicker.Date;
        var endCreationDate = CalendarEndCreationDatePicker.Date;
        SearchEverythingList = _tempList.Where(x =>
                x.UnityPackageName.StartsWith(textinput, StringComparison.InvariantCultureIgnoreCase) &&
                DateTime.Parse(x.CreatedTime.Replace("Creation Date: ", "")) >= creationDateStart &&
                DateTime.Parse(x.CreatedTime.Replace("Creation Date: ", "")) <= endCreationDate)
            .ToList();
        FoundText.Text = $"Found {SearchEverythingList.Count} results";
    }
}