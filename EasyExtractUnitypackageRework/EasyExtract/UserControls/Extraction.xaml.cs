using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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

    private ExtractionHelper ExtractionHelper { get; } = new();
    private ExtractionHandler ExtractionHandler { get; } = new();


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

    private static List<IgnoredUnitypackageModel>? IgnoredUnitypackages { get; } = new();
    public event PropertyChangedEventHandler PropertyChanged;


    private async Task PopulateExtractedFilesListAsync()
    {
        var directories = Directory.GetDirectories(ConfigModel.LastExtractedPath);
        foreach (var directory in directories)
        {
            var totalSizeInBytes = await Task.Run(async () => await CalculateDirectoryTotalSizeInBytesAsync(directory));
            var unitypackage =
                await Task.Run(async () => await CreateUnityPackageModelAsync(directory, totalSizeInBytes));
            await Task.Run(async () => await AddSubdirectoryItemsToUnityPackageAsync(unitypackage, directory));
            // Check if a unitypackage with the same name already exists in the collection
            if (ExtractedUnitypackages.All(u => u.UnitypackageName != unitypackage.UnitypackageName))
                Dispatcher.Invoke(() => { ExtractedUnitypackages.Add(unitypackage); });
        }
    }

    private Task<long> CalculateDirectoryTotalSizeInBytesAsync(string directory)
    {
        return Task.Run(() =>
        {
            return new DirectoryInfo(directory).GetFiles("*.*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        });
    }

    private Task<ExtractedUnitypackageModel> CreateUnityPackageModelAsync(string directory, long totalSizeInBytes)
    {
        return Task.Run(() => new ExtractedUnitypackageModel
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
        });
    }

    private Task AddSubdirectoryItemsToUnityPackageAsync(ExtractedUnitypackageModel unitypackage, string directory)
    {
        return Task.Run(() =>
        {
            var directoryInfo = new DirectoryInfo(directory);
            var files = directoryInfo.GetFiles("*.*", SearchOption.AllDirectories);
            var rootPath = directoryInfo.FullName;

            foreach (var file in files)
            {
                if (file.Name.EndsWith(".EASYEXTRACTPREVIEW.png")) continue;

                string category = null;

                // Use Dispatcher.Invoke to safely access the UI element from a non-UI thread
                Dispatcher.Invoke(() =>
                {
                    category = CategoryStructureBool?.IsChecked == true
                        ? ExtractionHelper.GetCategoryByExtension(file.Extension)
                        : GetFileRelativePath(file.FullName, rootPath);
                });

                unitypackage.SubdirectoryItems.Add(new ExtractedFiles
                {
                    FileName = file.Name,
                    FilePath = file.FullName,
                    Category = category,
                    Extension = file.Extension,
                    Size = ExtractionHelper.GetReadableFileSize(file.Length),
                    ExtractedDate = file.CreationTime,
                    SymbolIconImage = ExtractionHelper.GetSymbolByExtension(file.Extension),
                    PreviewImage = GeneratePreviewImage(file).Result
                });
            }
        });
    }

    private string GetFileRelativePath(string filePath, string rootPath)
    {
        return filePath.Replace(rootPath, "").TrimStart(Path.DirectorySeparatorChar);
    }


    private Task<BitmapImage?> GeneratePreviewImage(FileInfo fileInfo)
    {
        return Task.Run(() =>
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
        });
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void Extraction_OnLoaded(object sender, RoutedEventArgs e)
    {
        await CalculateScrollerHeightAsync();
        await UpdateDiscordPresenceState();

        await UpdateQueueHeaderAsync();
        await ChangeExtractionAnimationAsync();
        await UpdateInfoBadgesAsync();
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            Dispatcher.Invoke(() => { CategoryStructureBool.IsChecked = config.ExtractedCategoryStructure; });
            ConfigHelper.UpdateConfig(config);
        });
        UpdateExtractedFiles();
    }

    private async Task CalculateScrollerHeightAsync()
    {
        await Dispatcher.BeginInvoke((Action)(() =>
        {
            //max window height - height of the other elements
            var height = (int)ActualHeight - 200;
            ExtractedItemsScroller.MaxHeight = height;
        }));
    }

    private async Task UpdateInfoBadgesAsync()
    {
        TotalExtractedInExtractedFolder =
            (await Task.Run(() => Directory.GetDirectories(ConfigModel.LastExtractedPath))).Length.ToString();
        if (ManageExtractedInfoBadge != null) ManageExtractedInfoBadge.Value = TotalExtractedInExtractedFolder;
        await UpdateIgnoredUnitypackagesCountAsync();
    }

    private async Task UpdateIgnoredUnitypackagesCountAsync()
    {
        if (IgnoredUnitypackages == null)
        {
            ManageIgnoredInfoBadge.Value = "0";
            return;
        }

        await Dispatcher.BeginInvoke((Action)(() =>
        {
            if (ManageIgnoredInfoBadge != null) ManageIgnoredInfoBadge.Value = IgnoredUnitypackages.Count.ToString();
        }));
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

    private Task UpdateQueueHeaderAsync()
    {
        return Dispatcher.InvokeAsync(async () =>
        {
            switch (QueueListView.Items.Count)
            {
                case 0:
                    QueueExpander.Header = "Queue (Nothing to extract)";
                    ExtractionBtn.Visibility = Visibility.Collapsed;
                    await UpdateInfoBadgesAsync();
                    break;
                default:
                    QueueExpander.Header = QueueListView.Items.Count == 1
                        ? $"Queue ({QueueListView.Items.Count} Unitypackage)"
                        : $"Queue ({QueueListView.Items.Count} Unitypackage(s))";
                    ExtractionBtn.Visibility = Visibility.Visible;
                    await UpdateInfoBadgesAsync();
                    break;
            }
        }).Task;
    }

    private async Task ChangeExtractionAnimationAsync()
    {
        await Dispatcher.BeginInvoke((Action)(() =>
        {
            if (ExtractingIcon != null)
                AnimationBehavior.SetSourceUri(ExtractingIcon,
                    _isExtraction ? ExtractionAnimationUri : IdleAnimationUri);
        }));
    }

    private async void ExtractingIcon_OnSourceUpdated(object? sender, DataTransferEventArgs e)
    {
        await ChangeExtractionAnimationAsync();
    }

    private async void ExtractionBtn_OnClick(object sender, RoutedEventArgs e)
    {
        await ChangeExtractionAnimationAsync();
        _isExtraction = true;
        await SetupUiForExtractionAsync();

        var (ignoredCounter, fileFinishedCounter) = await ProcessUnityPackages();

        await UpdateUiAfterExtractionAsync(ignoredCounter, fileFinishedCounter);
        await UpdateInfoBadgesAsync();
    }

    private DispatcherOperation SetupUiForExtractionAsync()
    {
        return Dispatcher.InvokeAsync(() =>
        {
            StatusProgressBar.Maximum = QueueListView.Items.Count;
            StatusProgressBar.Visibility = Visibility.Visible;
            StatusBarDetailsTxt.Visibility = Visibility.Visible;
            StatusBar.Visibility = Visibility.Visible;
            ExtractionBtn.IsEnabled = false;
            ExtractionBtn.Appearance = ControlAppearance.Info;
        });
    }

    private async Task<(int ignoredCounter, int fileFinishedCounter)> ProcessUnityPackages()
    {
        var ignoredCounter = 0;
        var fileFinishedCounter = 0;
        foreach (var unitypackage in QueueListView.Items.Cast<SearchEverythingModel>())
        {
            var isValidUnityPackage = await IsValidUnityPackageAsync(unitypackage);
            if (!isValidUnityPackage.Item1)
            {
                ignoredCounter++;
                await AddToIgnoredUnitypackagesAsync(unitypackage, isValidUnityPackage.Item2);
                continue;
            }

            StatusBarText.Text = $"Extracting {unitypackage.UnityPackageName}...";
            if (await ExtractionHandler.ExtractUnitypackage(unitypackage))
            {
                fileFinishedCounter++;
                await UpdateExtractionProgressAsync(fileFinishedCounter);
            }
            else
            {
                ignoredCounter++;
                await AddToIgnoredUnitypackagesAsync(unitypackage, "Failed to extract");
            }
        }

        QueueListView.Items.Clear();
        await UpdateQueueHeaderAsync();
        await UpdateInfoBadgesAsync();
        ManageExtractedTab.IsSelected = true;
        return (ignoredCounter, fileFinishedCounter);
    }

    private Task<(bool, string)> IsValidUnityPackageAsync(SearchEverythingModel unitypackage)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(unitypackage.UnityPackagePath)) return (false, "File not found");

            var fileInfo = new FileInfo(unitypackage.UnityPackagePath);

            if (fileInfo.Extension != ".unitypackage") return (false, "File is not a Unitypackage");

            return fileInfo.Length == 0 ? (false, "File is empty") : (true, "");
        });
    }

    private Task AddToIgnoredUnitypackagesAsync(SearchEverythingModel unitypackage, string reason)
    {
        return Task.Run(async () =>
        {
            IgnoredUnitypackages.Add(new IgnoredUnitypackageModel
            {
                UnityPackageName = unitypackage.UnityPackageName,
                Reason = reason
            });
            await UpdateInfoBadgesAsync();
        });
    }

    private Task UpdateExtractionProgressAsync(int fileFinishedCounter)
    {
        return Task.Run(() =>
        {
            Dispatcher.Invoke(() =>
            {
                StatusBarDetailsTxt.Text = $"({fileFinishedCounter + 1}/{QueueListView.Items.Count})";
                StatusProgressBar.Value = fileFinishedCounter;
            });
        });
    }

    private Task UpdateUiAfterExtractionAsync(int ignoredCounter, int fileFinishedCounter)
    {
        return Task.Run(() =>
        {
            Dispatcher.InvokeAsync(async () =>
            {
                if (fileFinishedCounter == 0)
                {
                    StatusBarText.Text = $"No Unitypackages Extracted, {ignoredCounter} ignored.";
                    StatusProgressBar.Visibility = Visibility.Collapsed;
                    StatusBarDetailsTxt.Visibility = Visibility.Collapsed;
                    ExtractionBtn.IsEnabled = true;
                    ExtractionBtn.Appearance = ControlAppearance.Primary;
                    await ChangeExtractionAnimationAsync().ConfigureAwait(false);
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
                await ChangeExtractionAnimationAsync().ConfigureAwait(false);
                _isExtraction = false;
                UpdateExtractedFiles();
                StatusBar.Visibility = Visibility.Collapsed;
            });
            return Task.CompletedTask;
        });
    }

    private async void SearchFileManuallyButton_OnClick(object sender, RoutedEventArgs e)
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

            await UpdateQueueHeaderAsync();
        }
    }

    private async void Extraction_OnDrop(object sender, DragEventArgs e)
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

        await UpdateQueueHeaderAsync();
    }

    private async void Tabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //Update Info Badges
        await UpdateInfoBadgesAsync();
        //reset Extraction to normal
        await ChangeExtractionAnimationAsync();
        _isExtraction = false;
        UpdateExtractedFiles();
        StatusBar.Visibility = Visibility.Collapsed;
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

    private async void UpdateExtractedFiles()
    {
        Dispatcher.Invoke(() =>
        {
            ExtractedUnitypackages.Clear();
            CheckForDuplicateExtractedFiles();
        });
        await PopulateExtractedFilesListAsync();
    }

    private void CheckForDuplicateExtractedFiles()
    {
        foreach (var unitypackage in ExtractedUnitypackages)
        foreach (var extractedFile in unitypackage.SubdirectoryItems.Where(extractedFile =>
                     ExtractedUnitypackages.Any(x => x.UnitypackageName == extractedFile.FileName)).ToList())
            unitypackage.SubdirectoryItems.Remove(extractedFile);
    }
}