using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EasyExtract.Config;
using EasyExtract.Discord;
using EasyExtract.Extraction;
using Microsoft.Win32;
using XamlAnimatedGif;

namespace EasyExtract.UserControls;

public partial class Extraction : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _isExtraction;

    private static readonly Uri IdleAnimationUri =
        new("pack://application:,,,/EasyExtract;component/Resources/ExtractionProcess/Closed.png");

    private static readonly Uri ExtractionAnimationUri =
        new("pack://application:,,,/EasyExtract;component/Resources/Gifs/IconAnim.gif");

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static List<SearchEverythingModel>? _queueList { get; set; }

    public List<SearchEverythingModel>? QueueList
    {
        get => _queueList;
        set
        {
            if (_queueList == value) return;
            _queueList = value;
            OnPropertyChanged();
        }
    }

    public static List<IgnoredUnitypackageModel>? IgnoredUnitypackages { get; set; }


    public Extraction()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void Extraction_OnLoaded(object sender, RoutedEventArgs e)
    {
        await ChangeDiscordState();


        UpdateQueueHeader();
        ChangeExtractionAnimation();
    }

    private static async Task ChangeDiscordState()
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
                await DiscordRpcManager.Instance.UpdatePresenceAsync("Extraction");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
    }

    private void UpdateQueueHeader()
    {
        switch (QueueListView.Items.Count)
        {
            case 0:
                QueueExpander.Header = "Queue (Nothing to extract)";
                ExtractionBtn.Visibility = Visibility.Collapsed;
                break;
            default:
                QueueExpander.Header = QueueListView.Items.Count == 1
                    ? $"Queue ({QueueListView.Items.Count} Unitypackage)"
                    : $"Queue ({QueueListView.Items.Count} Unitypackage(s))";
                ExtractionBtn.Visibility = Visibility.Visible;
                break;
        }
    }

    private void ChangeExtractionAnimation()
    {
        AnimationBehavior.SetSourceUri(ExtractingIcon, _isExtraction ? ExtractionAnimationUri : IdleAnimationUri);
    }

    private void ExtractingIcon_OnSourceUpdated(object? sender, DataTransferEventArgs e)
    {
        ChangeExtractionAnimation();
    }

    private void ExtractionBtn_OnClick(object sender, RoutedEventArgs e)
    {
        _isExtraction = true;
        SetupUiForExtraction();

        var (ignoredCounter, fileFinishedCounter) = ProcessUnityPackages();

        UpdateUiAfterExtraction(ignoredCounter, fileFinishedCounter);
    }

    private void SetupUiForExtraction()
    {
        StatusProgressBar.Maximum = QueueListView.Items.Count;
        StatusProgressBar.Visibility = Visibility.Visible;
        StatusBarDetailsTxt.Visibility = Visibility.Visible;
        StatusBar.Visibility = Visibility.Visible;
        StatusBarManageExtractedBtn.Visibility = Visibility.Collapsed;
        StatusBarShowIgnoredBtn.Visibility = Visibility.Collapsed;
    }

    private (int ignoredCounter, int fileFinishedCounter) ProcessUnityPackages()
    {
        var ignoredCounter = 0;
        var fileFinishedCounter = 0;
        foreach (var unitypackage in QueueListView.Items.Cast<SearchEverythingModel>())
        {
            if (!IsValidUnityPackage(unitypackage, out var reason))
            {
                ignoredCounter++;
                AddToIgnoredUnitypackages(unitypackage, reason);
                continue;
            }

            StatusBarText.Text = $"Extracting {unitypackage.UnityPackageName}...";
            if (ExtractionHandler.ExtractUnitypackage(unitypackage))
            {
                fileFinishedCounter++;
                UpdateExtractionProgress(fileFinishedCounter);
            }
            else
            {
                ignoredCounter++;
                AddToIgnoredUnitypackages(unitypackage, "Failed to extract");
            }
        }

        return (ignoredCounter, fileFinishedCounter);
    }

    private bool IsValidUnityPackage(SearchEverythingModel unitypackage, out string reason)
    {
        if (!File.Exists(unitypackage.UnityPackagePath))
        {
            reason = "File not found";
            return false;
        }

        if (!unitypackage.UnityPackageName.EndsWith(".unitypackage"))
        {
            reason = "Not a Unitypackage";
            return false;
        }

        reason = "";
        return true;
    }

    private void AddToIgnoredUnitypackages(SearchEverythingModel unitypackage, string reason)
    {
        IgnoredUnitypackages?.Add(new IgnoredUnitypackageModel
        {
            UnityPackageName = unitypackage.UnityPackageName,
            Reason = reason
        });
    }

    private void UpdateExtractionProgress(int fileFinishedCounter)
    {
        ChangeExtractionAnimation();
        UpdateQueueHeader();
        StatusBarDetailsTxt.Text = $"({fileFinishedCounter + 1}/{QueueListView.Items.Count})";
        StatusProgressBar.Value = fileFinishedCounter;
    }

    private void UpdateUiAfterExtraction(int ignoredCounter, int fileFinishedCounter)
    {
        if (ignoredCounter > 0 || fileFinishedCounter > 0) StatusBarManageExtractedBtn.Visibility = Visibility.Visible;

        if (ignoredCounter > 0) StatusBarShowIgnoredBtn.Visibility = Visibility.Visible;

        if (fileFinishedCounter == 0)
        {
            StatusBarText.Text = $"No Unitypackages Extracted, {ignoredCounter} ignored.";
            StatusProgressBar.Visibility = Visibility.Collapsed;
            StatusBarDetailsTxt.Visibility = Visibility.Collapsed;
            StatusBarManageExtractedBtn.Visibility = Visibility.Collapsed;
            StatusBarShowIgnoredBtn.Visibility = Visibility.Visible;
            _isExtraction = false;
            ChangeExtractionAnimation();
            return;
        }

        StatusBarText.Text = fileFinishedCounter == 1
            ? $"Successfully extracted {fileFinishedCounter} Unitypackage, {ignoredCounter} ignored."
            : $"Successfully extracted {fileFinishedCounter} Unitypackages, {ignoredCounter} ignored.";
        StatusProgressBar.Visibility = Visibility.Collapsed;
        StatusBarDetailsTxt.Visibility = Visibility.Collapsed;
        _isExtraction = false;
        ChangeExtractionAnimation();
    }

    private void SearchFileManuallyButton_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Unitypackage files (*.unitypackage)|*.unitypackage",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            foreach (var fileName in openFileDialog.FileNames)
            {
                var duplicate = QueueListView.Items.Cast<SearchEverythingModel>()
                    .FirstOrDefault(x => x.UnityPackageName == Path.GetFileName(fileName));
                if (duplicate != null) continue;
                QueueListView.Items.Add(new SearchEverythingModel
                {
                    UnityPackageName = Path.GetFileName(fileName),
                    UnityPackagePath = fileName
                });
            }

            UpdateQueueHeader();
        }
    }
}