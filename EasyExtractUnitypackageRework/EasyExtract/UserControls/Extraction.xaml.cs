using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using EasyExtract.Config;
using EasyExtract.Discord;
using EasyExtract.Extraction;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using XamlAnimatedGif;

namespace EasyExtract.UserControls;

public partial class Extraction : UserControl, INotifyPropertyChanged
{
    private const string EasyExtractPreview = "EASYEXTRACTPREVIEW.png";

    private static readonly Uri IdleAnimationUri =
        new("pack://application:,,,/EasyExtract;component/Resources/ExtractionProcess/Closed.png");

    private static readonly Uri ExtractionAnimationUri =
        new("pack://application:,,,/EasyExtract;component/Resources/Gifs/IconAnim.gif");

    private ObservableCollection<ExtractedUnitypackageModel> _extractedUnitypackages = new();

    private bool _isExtraction;

    public Extraction()
    {
        InitializeComponent();
        DataContext = this;
    }

    private string TotalExtractedInExtractedFolder { get; set; }

    public ObservableCollection<ExtractedUnitypackageModel> ExtractedUnitypackages
    {
        get => _extractedUnitypackages;
        set
        {
            _extractedUnitypackages = value;
            OnPropertyChanged();
        }
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

    public static List<IgnoredUnitypackageModel>? IgnoredUnitypackages { get; set; } = new();
    public event PropertyChangedEventHandler PropertyChanged;


    private void PopulateExtractedFilesList()
    {
        var directories = Directory.GetDirectories(ConfigModel.LastExtractedPath);
        foreach (var directory in directories)
        {
            var totalSizeInBytes = CalculateDirectoryTotalSizeInBytes(directory);
            var unitypackage = CreateUnityPackageModel(directory, totalSizeInBytes);
            AddSubdirectoryItemsToUnityPackage(unitypackage, directory);
            ExtractedUnitypackages.Add(unitypackage);
        }
    }

    private long CalculateDirectoryTotalSizeInBytes(string directory)
    {
        return new DirectoryInfo(directory).GetFiles("*.*", SearchOption.AllDirectories)
            .Sum(file => file.Length);
    }

    private ExtractedUnitypackageModel CreateUnityPackageModel(string directory, long totalSizeInBytes)
    {
        return new ExtractedUnitypackageModel
        {
            UnitypackageName = Path.GetFileName(directory),
            UnitypackagePath = directory,
            UnitypackageSize = ExtractionHelper.GetReadableFileSize(totalSizeInBytes),
            UnitypackageTotalFileCount = ExtractionHelper.GetTotalFileCount(directory),
            UnitypackageTotalFolderCount = ExtractionHelper.GetTotalFolderCount(directory),
            UnitypackageTotalScriptCount = ExtractionHelper.GetTotalScriptCount(directory),
            UnitypackageTotalShaderCount = ExtractionHelper.GetTotalShaderCount(directory),
            UnitypackageTotalPrefabCount = ExtractionHelper.GetTotalPrefabCount(directory),
            UnitypackageTotal3DObjectCount = ExtractionHelper.GetTotal3DObjectCount(directory),
            UnitypackageTotalImageCount = ExtractionHelper.GetTotalImageCount(directory),
            UnitypackageTotalAudioCount = ExtractionHelper.GetTotalAudioCount(directory),
            UnitypackageTotalAnimationCount = ExtractionHelper.GetTotalAnimationCount(directory),
            UnitypackageTotalSceneCount = ExtractionHelper.GetTotalSceneCount(directory),
            UnitypackageTotalMaterialCount = ExtractionHelper.GetTotalMaterialCount(directory),
            UnitypackageTotalAssetCount = ExtractionHelper.GetTotalAssetCount(directory),
            UnitypackageTotalControllerCount = ExtractionHelper.GetTotalControllerCount(directory),
            UnitypackageTotalFontCount = ExtractionHelper.GetTotalFontCount(directory),
            UnitypackageTotalConfigurationCount = ExtractionHelper.GetTotalConfigurationCount(directory),
            UnitypackageTotalDataCount = ExtractionHelper.GetTotalDataCount(directory),
            UnitypackageExtractedDate = Directory.GetCreationTime(directory)
        };
    }

    private void AddSubdirectoryItemsToUnityPackage(ExtractedUnitypackageModel unitypackage, string directory)
    {
        var directoryInfo = new DirectoryInfo(directory);
        var files = directoryInfo.GetFiles("*.*", SearchOption.AllDirectories);
        var rootPath = directoryInfo.FullName;

        foreach (var file in files)
        {
            if (file.Name.EndsWith(".EASYEXTRACTPREVIEW.png")) continue;

            var category = CategoryStructureBool?.IsChecked == true
                ? ExtractionHelper.GetCategoryByExtension(file.Extension)
                : GetFileRelativePath(file.FullName, rootPath);

            unitypackage.SubdirectoryItems.Add(new ExtractedFiles
            {
                FileName = file.Name,
                FilePath = file.FullName,
                Category = category,
                Extension = file.Extension,
                Size = ExtractionHelper.GetReadableFileSize(file.Length),
                ExtractedDate = file.CreationTime,
                SymbolIconImage = ExtractionHelper.GetSymbolByExtension(file.Extension),
                PreviewImage = GeneratePreviewImage(file)
            });
        }
    }

    private string GetFileRelativePath(string filePath, string rootPath)
    {
        return filePath.Replace(rootPath, "").TrimStart(Path.DirectorySeparatorChar);
    }


    private BitmapImage? GeneratePreviewImage(FileInfo fileInfo)
    {
        var previewImagePath = Path.Combine(fileInfo.DirectoryName, $"{fileInfo.Name}.{EasyExtractPreview}");
        if (!File.Exists(previewImagePath)) return null;

        var previewImage = new BitmapImage();
        previewImage.BeginInit();
        previewImage.UriSource = new Uri(previewImagePath);
        previewImage.CacheOption = BitmapCacheOption.OnLoad;
        previewImage.EndInit();
        previewImage.Freeze();

        return previewImage;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void Extraction_OnLoaded(object sender, RoutedEventArgs e)
    {
        CalculateScrollerHeight();
        await UpdateDiscordPresenceState();

        UpdateQueueHeader();
        ChangeExtractionAnimation();
        UpdateInfoBadges();
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            Dispatcher.Invoke(() => { CategoryStructureBool.IsChecked = config.ExtractedCategoryStructure; });
            ConfigHelper.UpdateConfig(config);
        });
        UpdateExtractedFiles();
    }

    private void CalculateScrollerHeight()
    {
        //max window height - height of the other elements
        var height = (int)ActualHeight - 200;
        ExtractedItemsScroller.MaxHeight = height;
    }

    private void UpdateInfoBadges()
    {
        TotalExtractedInExtractedFolder = Directory.GetDirectories(ConfigModel.LastExtractedPath).Length.ToString();
        if (ManageExtractedInfoBadge != null) ManageExtractedInfoBadge.Value = TotalExtractedInExtractedFolder;
        UpdateIgnoredUnitypackagesCount();
    }

    private void UpdateIgnoredUnitypackagesCount()
    {
        if (IgnoredUnitypackages == null)
        {
            ManageIgnoredInfoBadge.Value = "0";
            return;
        }

        if (ManageIgnoredInfoBadge != null) ManageIgnoredInfoBadge.Value = IgnoredUnitypackages.Count.ToString();
    }

    private static async Task UpdateDiscordPresenceState()
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
                UpdateInfoBadges();
                break;
            default:
                QueueExpander.Header = QueueListView.Items.Count == 1
                    ? $"Queue ({QueueListView.Items.Count} Unitypackage)"
                    : $"Queue ({QueueListView.Items.Count} Unitypackage(s))";
                ExtractionBtn.Visibility = Visibility.Visible;
                UpdateInfoBadges();
                break;
        }
    }

    private void ChangeExtractionAnimation()
    {
        if (ExtractingIcon != null)
            AnimationBehavior.SetSourceUri(ExtractingIcon, _isExtraction ? ExtractionAnimationUri : IdleAnimationUri);
    }

    private void ExtractingIcon_OnSourceUpdated(object? sender, DataTransferEventArgs e)
    {
        ChangeExtractionAnimation();
    }

    private async void ExtractionBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ChangeExtractionAnimation();
        _isExtraction = true;
        SetupUiForExtraction();

        var (ignoredCounter, fileFinishedCounter) = await ProcessUnityPackages();

        UpdateUiAfterExtraction(ignoredCounter, fileFinishedCounter);
        UpdateInfoBadges();
    }

    private void SetupUiForExtraction()
    {
        StatusProgressBar.Maximum = QueueListView.Items.Count;
        StatusProgressBar.Visibility = Visibility.Visible;
        StatusBarDetailsTxt.Visibility = Visibility.Visible;
        StatusBar.Visibility = Visibility.Visible;
        ExtractionBtn.IsEnabled = false;
        ExtractionBtn.Appearance = ControlAppearance.Info;
    }

    private async Task<(int ignoredCounter, int fileFinishedCounter)> ProcessUnityPackages()
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
            if (await ExtractionHandler.ExtractUnitypackage(unitypackage))
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

        QueueListView.Items.Clear();
        UpdateQueueHeader();
        UpdateInfoBadges();
        ManageExtractedTab.IsSelected = true;
        return (ignoredCounter, fileFinishedCounter);
    }

    private bool IsValidUnityPackage(SearchEverythingModel unitypackage, out string reason)
    {
        if (!File.Exists(unitypackage.UnityPackagePath))
        {
            reason = "File not found";
            return false;
        }

        var fileInfo = new FileInfo(unitypackage.UnityPackagePath);

        if (fileInfo.Extension != ".unitypackage")
        {
            reason = "File is not a Unitypackage";
            return false;
        }

        if (fileInfo.Length == 0)
        {
            reason = "File is empty";
            return false;
        }

        reason = "";
        return true;
    }

    private void AddToIgnoredUnitypackages(SearchEverythingModel unitypackage, string reason)
    {
        IgnoredUnitypackages.Add(new IgnoredUnitypackageModel
        {
            UnityPackageName = unitypackage.UnityPackageName,
            Reason = reason
        });
        UpdateInfoBadges();
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
        if (fileFinishedCounter == 0)
        {
            StatusBarText.Text = $"No Unitypackages Extracted, {ignoredCounter} ignored.";
            StatusProgressBar.Visibility = Visibility.Collapsed;
            StatusBarDetailsTxt.Visibility = Visibility.Collapsed;
            ExtractionBtn.IsEnabled = true;
            ExtractionBtn.Appearance = ControlAppearance.Primary;
            ChangeExtractionAnimation();
            _isExtraction = false;
            UpdateExtractedFiles();
            return;
        }

        ExtractionBtn.IsEnabled = true;
        ExtractionBtn.Appearance = ControlAppearance.Primary;


        StatusBarText.Text = fileFinishedCounter == 1
            ? $"Successfully extracted {fileFinishedCounter} Unitypackage, {ignoredCounter} ignored."
            : $"Successfully extracted {fileFinishedCounter} Unitypackages, {ignoredCounter} ignored.";
        StatusProgressBar.Visibility = Visibility.Collapsed;
        StatusBarDetailsTxt.Visibility = Visibility.Collapsed;
        ChangeExtractionAnimation();
        _isExtraction = false;
        UpdateExtractedFiles();
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

    private void Extraction_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var fileName in files)
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

    private void Tabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //Update Info Badges
        UpdateInfoBadges();
        //reset Extraction to normal
        ChangeExtractionAnimation();
        _isExtraction = false;
        UpdateExtractedFiles();
    }

    private async void CategoryStructureBool_OnChecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.ExtractedCategoryStructure = true;
            ConfigHelper.UpdateConfig(config);
        });
        UpdateExtractedFiles();
    }

    private async void CategoryStructureBool_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.ExtractedCategoryStructure = false;
            ConfigHelper.UpdateConfig(config);
        });

        UpdateExtractedFiles();
    }

    private void UpdateExtractedFiles()
    {
        ExtractedUnitypackages.Clear();
        CheckForDuplicateExtractedFiles();
        PopulateExtractedFilesList();
    }

    private void CheckForDuplicateExtractedFiles()
    {
        foreach (var unitypackage in ExtractedUnitypackages)
        foreach (var extractedFile in unitypackage.SubdirectoryItems.Where(extractedFile =>
                     ExtractedUnitypackages.Any(x => x.UnitypackageName == extractedFile.FileName)).ToList())
            unitypackage.SubdirectoryItems.Remove(extractedFile);
    }
}