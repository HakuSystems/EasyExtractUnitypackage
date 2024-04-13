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
    private readonly List<SearchEverythingModel> _tempList = new();

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private List<SearchEverythingModel>? _searchEverythingList;

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

    public SearchEverything()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void SearchEverything_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!EverythingValidation.AreSystemRequirementsMet())
        {
            SearchEverythingCard.Visibility = Visibility.Collapsed;
            FallbackEverything.Visibility = Visibility.Visible;
            FallbackEverything.Text = EverythingValidation.AreSystemRequirementsMetString();
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
        }


        #region Discord

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
                await DiscordRpcManager.Instance.UpdatePresenceAsync("Search Everything");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }

        #endregion
    }

    private void LoopList()
    {
        uint i;

        for (i = 0; i < Everything.Everything_GetNumResults(); i++)
        {
            var path = Marshal.PtrToStringUni(Everything.Everything_GetResultFullPathName(i));
            var name = Marshal.PtrToStringUni(Everything.Everything_GetResultFileName(i));
            var duplicate = _tempList.Find(x => x.UnityPackageName == name);
            if (duplicate != null) continue;
            if (path != null)
                _tempList.Add(new SearchEverythingModel { UnityPackageName = name, UnityPackagePath = path, Id = i });
        }
    }

    private void SearchEverythingTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(SearchEverythingTextBox.Text))
        {
            SearchEverythingList = null;
            FoundText.Text = "Search for a UnityPackage Name";
            return;
        }

        SearchEverythingList = _tempList.Where(x =>
                x.UnityPackageName.StartsWith(SearchEverythingTextBox.Text,
                    StringComparison.InvariantCultureIgnoreCase))
            .ToList();
        FoundText.Text = $"Found {SearchEverythingList.Count} results";
    }

    private void SearchFileManuallyButton_OnClick(object sender, RoutedEventArgs e)
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
                    { UnityPackageName = name, UnityPackagePath = file, Id = 0 });

                AddedStatusTxt.Text = counter switch
                {
                    1 => $"Added {name} to the queue",
                    > 1 => $"Added {counter} UnityPackage(s) to the queue",
                    _ => AddedStatusTxt.Text
                };
            }
        }
    }

    private void QueueAddButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SearchEverythingList == null) return;
        var selected = SearchEverythingList.FirstOrDefault();
        if (selected == null) return;
        var id = selected.Id;
        var name = selected.UnityPackageName;
        var path = selected.UnityPackagePath;

        var duplicate = Extraction._queueList?.Find(x => x.UnityPackageName == name);
        if (duplicate != null) return;
        if (Extraction._queueList == null) Extraction._queueList = new List<SearchEverythingModel>();
        Extraction._queueList.Add(new SearchEverythingModel
            { UnityPackageName = name, UnityPackagePath = path, Id = id });
        AddedStatusTxt.Text = $"Added {name} to the queue";
    }
}