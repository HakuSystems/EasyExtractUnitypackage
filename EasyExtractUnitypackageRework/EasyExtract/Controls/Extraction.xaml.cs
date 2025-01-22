using System.Collections.ObjectModel;
using System.Windows.Media;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;
using LiveCharts;
using LiveCharts.Wpf;
using Wpf.Ui.Controls;
using Brushes = System.Windows.Media.Brushes;

namespace EasyExtract.Controls;

public partial class Extraction : UserControl, INotifyPropertyChanged
{
    /// <summary>
    ///     Represents the EasyExtractPreview constant which is used as the file extension for preview images.
    /// </summary>
    private const string EasyExtractPreview = "EASYEXTRACTPREVIEW.png";

    /// <summary>
    ///     Represents a collection of extracted Unitypackages.
    /// </summary>
    private ObservableCollection<ExtractedUnitypackageModel> _extractedUnitypackages = new();

    /// The boolean variable _isExtraction represents whether an extraction process is currently happening or not.
    private bool _isExtraction;

    public Extraction()
    {
        InitializeComponent();
        DataContext = this;
    }


    /// <summary>
    ///     The ExtractionHelper class provides various helper methods for extracting information from directories.
    /// </summary>
    private ExtractionHelper ExtractionHelper { get; } = new();

    /// <summary>
    ///     Represents a class that handles the extraction of Unitypackages.
    /// </summary>
    private ExtractionHandler ExtractionHandler { get; } = new();

    /// <summary>
    ///     Represents the total number of extracted items in the extracted folder.
    /// </summary>
    private string TotalExtractedInExtractedFolder { get; set; }

    /// <summary>
    ///     Represents a collection of extracted Unitypackages.
    /// </summary>
    public ObservableCollection<ExtractedUnitypackageModel> ExtractedUnitypackages
    {
        get => _extractedUnitypackages;
        set
        {
            _extractedUnitypackages = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets or sets the list of SearchEverythingModel's in the queue.
    /// </summary>
    public static List<SearchEverythingModel>? SearchResultQueue { get; set; }

    /// <summary>
    ///     Represents a queue list of SearchEverythingModel objects.
    /// </summary>
    public static List<SearchEverythingModel>? QueueList
    {
        get => SearchResultQueue;
        set
        {
            if (SearchResultQueue == value) return;
            SearchResultQueue = value;
        }
    }

    /// <summary>
    ///     Gets or sets the list of ignored Unitypackages.
    /// </summary>
    public ObservableCollection<IgnoredPackageInventory> IgnoredUnitypackages { get; set; } = new();

    public event PropertyChangedEventHandler PropertyChanged;

    private async void UpdateChart()
    {
        var collection = new SeriesCollection();
        var extractedUnitypackages = ExtractedUnitypackages.ToList();

        var categories = new[]
        {
            "Scripts",
            "Shaders",
            "Prefabs",
            "3D Objects",
            "Images",
            "Audios",
            "Animations",
            "Scenes",
            "Materials",
            "Assets",
            "Controllers",
            "Fonts",
            "Configurations",
            "Data"
        };

        var colors = new[]
        {
            Brushes.Red,
            Brushes.DarkBlue,
            Brushes.Gold,
            Brushes.LimeGreen,
            Brushes.MediumOrchid,
            Brushes.OrangeRed,
            Brushes.DarkTurquoise,
            Brushes.LightPink,
            Brushes.DarkSlateBlue,
            Brushes.Brown,
            Brushes.ForestGreen,
            Brushes.DeepPink,
            Brushes.YellowGreen,
            Brushes.SteelBlue,
            Brushes.Sienna,
            Brushes.Tan,
            Brushes.Plum,
            Brushes.LightSalmon
        };

        var counts = new[]
        {
            extractedUnitypackages.Sum(x => x.UnitypackageTotalScriptCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotalShaderCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotalPrefabCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotal3DObjectCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotalImageCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotalAudioCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotalAnimationCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotalSceneCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotalMaterialCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotalAssetCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotalControllerCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotalFontCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotalConfigurationCount),
            extractedUnitypackages.Sum(x => x.UnitypackageTotalDataCount)
        };

        for (var i = 0; i < categories.Length; i++)
            collection.Add(new ColumnSeries
            {
                Title = categories[i],
                Values = new ChartValues<double>
                {
                    counts[i]
                },
                Fill = colors[i],
                DataLabels = true,
                FontSize = 12,
                FontWeight = FontWeights.Bold
            });


        UnityPackageChart.Series = collection;
    }

    /// <summary>
    ///     Populates the list of extracted files asynchronously.
    /// </summary>
    private async Task PopulateExtractedFilesListAsync()
    {
        // Populate the list of extracted Unitypackages from the extracted folder
        // Get the directories in the extraction path
        var directories = Directory.GetDirectories(ConfigHandler.Instance.Config.LastExtractedPath);
        foreach (var directory in directories)
        {
            StatusBar.Visibility = Visibility.Visible;
            StatusProgressBar.Visibility = Visibility.Collapsed;
            StatusBarText.Text = $"Loading {Path.GetFileName(directory)} Information...";
            // Calculate total size and create a model for the Unitypackage.
            var totalSizeInBytes = await Task.Run(async () => await CalculateDirectoryTotalSizeInBytesAsync(directory));
            var unitypackage =
                await Task.Run(async () => await CreateUnityPackageModelAsync(directory, totalSizeInBytes));
            // Add subdirectory items to the package model.
            await Task.Run(async () => await AddSubdirectoryItemsToUnityPackageAsync(unitypackage, directory));
            // Avoid duplicate packages by checking if it already exists
            Dispatcher.InvokeAsync(() =>
            {
                if (ExtractedUnitypackages.All(u => u.UnitypackageName != unitypackage.UnitypackageName))
                    ExtractedUnitypackages.Add(unitypackage);
            });
            StatusBar.Visibility = Visibility.Collapsed;
            StatusProgressBar.Visibility = Visibility.Visible;
        }

        await BetterLogger.LogAsync("Populated Extracted Files List", Importance.Info);
    }

    /// <summary>
    ///     Calculates the total size of a directory in bytes asynchronously.
    /// </summary>
    /// <param name="directory">The directory path.</param>
    /// <returns>The total size of the directory in bytes.</returns>
    private Task<long> CalculateDirectoryTotalSizeInBytesAsync(string directory)
    {
        return Task.Run(() =>
        {
            return new DirectoryInfo(directory).GetFiles("*.*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        });
    }

    /// <summary>
    ///     Creates a model for the Unitypackage based on the provided directory and total size in bytes.
    /// </summary>
    /// <param name="directory">The path of the directory for which the Unitypackage model is created.</param>
    /// <param name="totalSizeInBytes">The total size of the directory in bytes.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created Unitypackage model.</returns>
    private Task<ExtractedUnitypackageModel> CreateUnityPackageModelAsync(string directory, long totalSizeInBytes)
    {
        return Task.Run(async () => new ExtractedUnitypackageModel
        {
            UnitypackageName = Path.GetFileName(directory),
            UnitypackagePath = directory,
            UnitypackageSize = await ExtractionHelper.GetReadableFileSize(totalSizeInBytes),
            UnitypackageTotalFileCount = await ExtractionHelper.GetTotalFileCount(directory),
            UnitypackageTotalFolderCount = await ExtractionHelper.GetTotalFolderCount(directory),
            UnitypackageTotalScriptCount = await ExtractionHelper.GetTotalScriptCount(directory),

            #region MALICIOUS CODE DETECTION

            MalicousDiscordWebhookCount = await ExtractionHelper.GetMalicousDiscordWebhookCount(directory),
            LinkDetectionCount = await ExtractionHelper.GetTotalLinkDetectionCount(directory),

            #endregion

            UnitypackageTotalShaderCount = await ExtractionHelper.GetTotalShaderCount(directory),
            UnitypackageTotalPrefabCount = await ExtractionHelper.GetTotalPrefabCount(directory),
            UnitypackageTotal3DObjectCount = await ExtractionHelper.GetTotal3DObjectCount(directory),
            UnitypackageTotalImageCount = await ExtractionHelper.GetTotalImageCount(directory),
            UnitypackageTotalAudioCount = await ExtractionHelper.GetTotalAudioCount(directory),
            UnitypackageTotalAnimationCount = await ExtractionHelper.GetTotalAnimationCount(directory),
            UnitypackageTotalSceneCount = await ExtractionHelper.GetTotalSceneCount(directory),
            UnitypackageTotalMaterialCount = await ExtractionHelper.GetTotalMaterialCount(directory),
            UnitypackageTotalAssetCount = await ExtractionHelper.GetTotalAssetCount(directory),
            UnitypackageTotalControllerCount = await ExtractionHelper.GetTotalControllerCount(directory),
            UnitypackageTotalFontCount = await ExtractionHelper.GetTotalFontCount(directory),
            UnitypackageTotalConfigurationCount = await ExtractionHelper.GetTotalConfigurationCount(directory),
            UnitypackageTotalDataCount = await ExtractionHelper.GetTotalDataCount(directory),
            UnitypackageExtractedDate = Directory.GetCreationTime(directory)
        });
    }

    /// <summary>
    ///     Adds subdirectory items to the Unitypackage model.
    /// </summary>
    /// <param name="unitypackage">The Unitypackage model.</param>
    /// <param name="directory">The directory to search for subdirectory items.</param>
    private Task AddSubdirectoryItemsToUnityPackageAsync(ExtractedUnitypackageModel unitypackage, string directory)
    {
        return Task.Run(async () =>
        {
            var directoryInfo = new DirectoryInfo(directory);
            var files = directoryInfo.GetFiles("*.*", SearchOption.AllDirectories);
            var rootPath = directoryInfo.FullName;

            foreach (var file in files)
            {
                if (file.Name.EndsWith(".EASYEXTRACTPREVIEW.png")) continue;

                string category = null;

                Dispatcher.Invoke(async () =>
                {
                    category = CategoryStructureBool?.IsChecked == true
                        ? await ExtractionHelper.GetCategoryByExtension(file.Extension)
                        : GetFileRelativePath(file.FullName, rootPath);
                });

                unitypackage.SubdirectoryItems.Add(new ExtractedFiles
                {
                    FileName = file.Name,
                    FilePath = file.FullName,
                    Category = category,
                    Extension = file.Extension,
                    Size = await ExtractionHelper.GetReadableFileSize(file.Length),
                    ExtractedDate = file.CreationTime,
                    SymbolIconImage = await ExtractionHelper.GetSymbolByExtension(file.Extension),
                    PreviewImage = GeneratePreviewImage(file).Result
                });
            }
        });
    }

    /// <summary>
    ///     Returns the relative path of a file from the specified root path.
    /// </summary>
    /// <param name="filePath">The full path of the file.</param>
    /// <param name="rootPath">The root path within which the file is located.</param>
    /// <returns>The relative path of the file from the root path.</returns>
    private string GetFileRelativePath(string filePath, string rootPath)
    {
        return filePath.Replace(rootPath, "").TrimStart(Path.DirectorySeparatorChar);
    }


    /// <summary>
    ///     Generates a preview image for given FileInfo.
    /// </summary>
    /// <param name="fileInfo">The FileInfo object representing the file for which to generate the preview image.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result is a BitmapImage object representing the
    ///     generated preview image, or null if the preview image doesn't exist.
    /// </returns>
    private async Task<BitmapImage?> GeneratePreviewImage(FileInfo fileInfo)
    {
        return await Task.Run(() =>
        {
            var previewImagePath = Path.Combine(fileInfo.DirectoryName, $"{fileInfo.Name}.{EasyExtractPreview}");
            if (File.Exists(previewImagePath))
            {
                try
                {
                    using (var fileStream =
                           new FileStream(previewImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var previewImage = new BitmapImage();
                        previewImage.BeginInit();
                        previewImage.StreamSource = fileStream;
                        previewImage.CacheOption = BitmapCacheOption.OnLoad;
                        previewImage.EndInit();
                        previewImage.Freeze();

                        return previewImage;
                    }
                }
                catch (Exception ex)
                {
                    // Log exception here
                    Console.WriteLine($"Error loading image: {ex.Message}");
                }

                return null;
            }

            if (fileInfo.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
                fileInfo.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                fileInfo.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                fileInfo.Extension.Equals(".shader", StringComparison.OrdinalIgnoreCase))
            {
                // Convert code to image
                var code = File.ReadAllText(fileInfo.FullName);
                var codeImage = CodeToImageConverter.ConvertCodeToImage(code);
                if (codeImage != null)
                {
                    CodeToImageConverter.SaveImageToFile(codeImage, previewImagePath);
                    return codeImage;
                }
            }

            return null;
        });
    }

    /// <summary>
    ///     Raises the PropertyChanged event when a property value changes.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    ///     Event handler for the Loaded event of the Extraction UserControl. This method is responsible for performing various
    ///     initialization tasks when the user control is loaded.
    /// </summary>
    /// <param name="sender">The object that raised the event.</param>
    /// <param name="e">The event data.</param>
    private async void Extraction_OnLoaded(object sender, RoutedEventArgs e)
    {
        await CalculateScrollerHeightAsync();
        await DiscordRpcManager.Instance.TryUpdatePresenceAsync("Extraction");

        await UpdateQueueHeaderAsync();
        await UpdateInfoBadgesAsync();
        Dispatcher.Invoke(() =>
        {
            CategoryStructureBool.IsChecked = ConfigHandler.Instance.Config.ExtractedCategoryStructure;
        });
        await UpdateExtractedFiles();
        await UpdateIgnoredConfigListAsync();
        if (ExtractedUnitypackages.Count == 0)
            ExtractionTab.IsSelected = true;
        else
            ManageExtractedTab.IsSelected = true;
        UpdateSelectAllToggleContent();
        UpdateChart();
    }

    private void UpdateSelectAllToggleContent()
    {
        SelectAllUnitypackageToggle.Content = ExtractedUnitypackages.Count > 0
            ? $"Select All {ExtractedUnitypackages.Count}"
            : "Select All Unitypackages";
        UpdateChart();
    }

    private async Task UpdateIgnoredConfigListAsync()
    {
        if (ConfigHandler.Instance.Config == null) return;

        var ignoredAppDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract", "IgnoredUnitypackages");
        Directory.CreateDirectory(ignoredAppDataDirectory);

        var directoryNames = Directory.GetDirectories(ignoredAppDataDirectory).Select(Path.GetFileName).ToList();

        var packagesToRemove = ConfigHandler.Instance.Config.IgnoredUnityPackages
            .Where(package => !directoryNames.Contains(package.IgnoredUnityPackageName)).ToList();

        foreach (var packageToRemove in packagesToRemove)
            ConfigHandler.Instance.Config.IgnoredUnityPackages.Remove(packageToRemove);
        if (packagesToRemove.Any())
            await

                // Update UI with existing ignored packages
                Dispatcher.InvokeAsync(() =>
                {
                    IgnoredUnitypackages.Clear();
                    foreach (var ignoredUnitypackage in ConfigHandler.Instance.Config.IgnoredUnityPackages)
                        if (directoryNames.Contains(ignoredUnitypackage.IgnoredUnityPackageName))
                            IgnoredUnitypackages.Add(ignoredUnitypackage);
                });

        // Handle directories not listed in the config
        var packagesToAdd = new List<IgnoredPackageInventory>();
        foreach (var newIgnoredPackage in from directory in directoryNames
                 where ConfigHandler.Instance.Config.IgnoredUnityPackages.All(p =>
                     p.IgnoredUnityPackageName != directory)
                 select new IgnoredPackageInventory
                 {
                     IgnoredUnityPackageName = directory,
                     IgnoredReason = "Unknown Reason"
                 })
        {
            packagesToAdd.Add(newIgnoredPackage);
            ConfigHandler.Instance.Config.IgnoredUnityPackages.Add(newIgnoredPackage);
        }

        // Update the config file if there were new packages added
        if (packagesToAdd.Any())
            await

                // Update the UI with newly added packages
                Dispatcher.InvokeAsync(() =>
                {
                    foreach (var newPackage in packagesToAdd) IgnoredUnitypackages.Add(newPackage);
                });
        UpdateSelectAllToggleContent();
    }


    /// <summary>
    ///     Asynchronously calculates the height of the scroller in the Extraction user control.
    /// </summary>
    private async Task CalculateScrollerHeightAsync()
    {
        await Dispatcher.BeginInvoke((Action)(() =>
        {
            //max window height - height of the other elements
            var height = (int)ActualHeight - 200;
            ExtractedItemsScroller.MaxHeight = height;
        }));
    }

    /// <summary>
    ///     Asynchronously updates the information badges related to extraction.
    /// </summary>
    private async Task UpdateInfoBadgesAsync()
    {
        TotalExtractedInExtractedFolder =
            (await Task.Run(() => Directory.GetDirectories(ConfigHandler.Instance.Config.LastExtractedPath))).Length
            .ToString();
        if (ManageExtractedInfoBadge != null) ManageExtractedInfoBadge.Value = TotalExtractedInExtractedFolder;
        await UpdateIgnoredUnitypackagesCountAsync();
        UpdateSelectAllToggleContent();
    }

    /// <summary>
    ///     Updates the count of ignored Unitypackages.
    /// </summary>
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


    /// <summary>
    ///     Updates the header of the queue asynchronously based on the current count of items in the queue.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation of updating the queue header and visibility of the extraction
    ///     button.
    /// </returns>
    private Task UpdateQueueHeaderAsync()
    {
        return Dispatcher.InvokeAsync(async () =>
        {
            switch (QueueListView.Items.Count)
            {
                case 0:
                    QueueHeaderText.Text = "Queue (Nothing to extract)";
                    ExtractionBtn.Visibility = Visibility.Collapsed; // Hides the extraction button
                    await UpdateInfoBadgesAsync();
                    break;
                default:
                    QueueHeaderText.Text = QueueListView.Items.Count == 1
                        ? $"Queue ({QueueListView.Items.Count} Unitypackage)"
                        : $"Queue ({QueueListView.Items.Count} Unitypackage(s))";
                    ExtractionBtn.Visibility = Visibility.Visible; // Shows the extraction button
                    await UpdateInfoBadgesAsync();
                    break;
            }
        }).Task;
    }


    private async void ExtractionBtn_OnClick(object sender, RoutedEventArgs e)
    {
        _isExtraction = true;
        await SetupUiForExtractionAsync();

        var (ignoredCounter, fileFinishedCounter) = await ProcessUnityPackages();
        await BetterLogger.LogAsync(
            $"Extraction Process Completed: {fileFinishedCounter} packages extracted, {ignoredCounter} packages ignored",
            Importance.Info);

        await UpdateUiAfterExtractionAsync(ignoredCounter, fileFinishedCounter);
        await UpdateInfoBadgesAsync();
        QueueListView.Items.Clear();
        await UpdateQueueHeaderAsync();
    }

    /// <summary>
    ///     Sets up the user interface for the extraction process asynchronously.
    /// </summary>
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

    /// <summary>
    ///     Processes the Unitypackages in the queue and extracts them.
    /// </summary>
    /// <returns>
    ///     Returns a tuple containing the number of ignored Unitypackages and the number of successfully extracted
    ///     Unitypackages.
    /// </returns>
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
                await AddUnitypackageToHistoryAsync(unitypackage);
                await UpdateExtractionProgressAsync(fileFinishedCounter);
            }
            else
            {
                ignoredCounter++;
                await AddToIgnoredUnitypackagesAsync(unitypackage, "Failed to extract");
            }
        }

        _extractedUnitypackages.Clear();
        await UpdateQueueHeaderAsync();
        await UpdateInfoBadgesAsync();
        ManageExtractedTab.IsSelected = true;
        return (ignoredCounter, fileFinishedCounter);
    }

    private async Task AddUnitypackageToHistoryAsync(SearchEverythingModel unitypackage)
    {
        ConfigHandler.Instance.Config.History.Add(new HistoryModel
        {
            FileName = unitypackage.UnityPackageName,
            ExtractedPath =
                Path.Combine(ConfigHandler.Instance.Config.LastExtractedPath, unitypackage.UnityPackageName),
            ExtractedDate = DateTime.Now,
            TotalFiles = Directory.GetFiles(
                Path.Combine(ConfigHandler.Instance.Config.LastExtractedPath, unitypackage.UnityPackageName),
                "*.*", SearchOption.AllDirectories).Length
        });
    }

    /// <summary>
    ///     Determines whether a Unitypackage is valid or not.
    /// </summary>
    /// <param name="unitypackage">The Unitypackage to check.</param>
    /// <returns>
    ///     Returns a tuple with the first item indicating whether the Unitypackage is valid or not, and the second item
    ///     containing a message explaining the reason if the package is not valid.
    /// </returns>
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

    /// <summary>
    ///     Adds the specified unitypackage to the ignored unitypackages list with the given reason.
    /// </summary>
    /// <param name="unitypackage">The unitypackage to be ignored.</param>
    /// <param name="reason">The reason for ignoring the unitypackage.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    private Task AddToIgnoredUnitypackagesAsync(SearchEverythingModel unitypackage, string reason)
    {
        return Dispatcher.InvokeAsync(async () =>
        {
            IgnoredUnitypackages.Add(new IgnoredPackageInventory
            {
                IgnoredUnityPackageName = unitypackage.UnityPackageName,
                IgnoredReason = reason
            });
            await MoveIgnoredUnitypackageAsync(unitypackage);
            await CreateIgnoredConfigFileAsync(unitypackage, reason);
            await UpdateInfoBadgesAsync();
        }).Task;
    }

    private async Task CreateIgnoredConfigFileAsync(SearchEverythingModel unitypackage, string reason)
    {
        ConfigHandler.Instance.Config.IgnoredUnityPackages.Add(new IgnoredPackageInventory
        {
            IgnoredUnityPackageName = unitypackage.UnityPackageName,
            IgnoredReason = reason
        });
    }

    private async Task MoveIgnoredUnitypackageAsync(SearchEverythingModel unitypackage)
    {
        var ignoredAppDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract", "IgnoredUnitypackages");
        if (!Directory.Exists(ignoredAppDataDirectory)) Directory.CreateDirectory(ignoredAppDataDirectory);

        var ignoredUnitypackagePath = Path.Combine(ignoredAppDataDirectory, unitypackage.UnityPackageName);

        // Check if the destination already exists and delete it if it does
        if (Directory.Exists(ignoredUnitypackagePath) || File.Exists(ignoredUnitypackagePath))
            Directory.Delete(ignoredUnitypackagePath, true);

        await Task.Run(() => Directory.Move(unitypackage.UnityPackagePath, ignoredUnitypackagePath));
    }


    /// <summary>
    ///     Updates the extraction progress in the status bar.
    /// </summary>
    /// <param name="fileFinishedCounter">The number of files that have finished extraction.</param>
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

    /// <summary>
    ///     Updates the user interface after the extraction process.
    /// </summary>
    /// <param name="ignoredCounter">The number of Unitypackages ignored during the extraction process.</param>
    /// <param name="fileFinishedCounter">The number of Unitypackages successfully extracted.</param>
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
                    UpdateSelectAllToggleContent();
                    _isExtraction = false;
                    await UpdateExtractedFiles();
                    return;
                }

                ExtractionBtn.IsEnabled = true;
                ExtractionBtn.Appearance = ControlAppearance.Primary;

                StatusBarText.Text = fileFinishedCounter == 1
                    ? $"Successfully extracted {fileFinishedCounter} Unitypackage, {ignoredCounter} ignored."
                    : $"Successfully extracted {fileFinishedCounter} Unitypackages, {ignoredCounter} ignored.";
                StatusProgressBar.Visibility = Visibility.Collapsed;
                StatusBarDetailsTxt.Visibility = Visibility.Collapsed;
                UpdateSelectAllToggleContent();
                _isExtraction = false;
                await UpdateExtractedFiles();
                StatusBar.Visibility = Visibility.Collapsed;
            });
            return Task.CompletedTask;
        });
    }

    /// <summary>
    ///     Handles the click event of the SearchFileManuallyButton.
    ///     Allows the user to manually select files to search for extraction.
    /// </summary>
    /// <param name="sender">The object that raised the event.</param>
    /// <param name="e">The event arguments.</param>
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

        await BetterLogger.LogAsync("Manually Searched and Added Files", Importance.Info);
    }

    /// <summary>
    ///     Event handler for the Drop event of the Extraction user control.
    /// </summary>
    /// <param name="sender">The object that raised the event.</param>
    /// <param name="e">A DragEventArgs that contains the event data.</param>
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
        await BetterLogger.LogAsync("Dropped Files Added to Queue", Importance.Info);
    }

    /// <summary>
    ///     Event handler for the SelectionChanged event of the Tabs control.
    /// </summary>
    /// <param name="sender">The object that raised the event.</param>
    /// <param name="e">A SelectionChangedEventArgs object that contains the event data.</param>
    private async void Tabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //Update Info Badges
        await UpdateInfoBadgesAsync();
        //reset Extraction to normal
        _isExtraction = false;
        await UpdateExtractedFiles();
        StatusBar.Visibility = Visibility.Collapsed;
    }

    private async void CategoryStructureBool_OnChecked(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.ExtractedCategoryStructure = true;
        await UpdateExtractedFiles();
    }

    private async void CategoryStructureBool_OnUnchecked(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.ExtractedCategoryStructure = false;

        await UpdateExtractedFiles();
    }

    private async Task UpdateExtractedFiles()
    {
        Dispatcher.InvokeAsync(() =>
        {
            ExtractedUnitypackages.Clear();
            CheckForDuplicateExtractedFiles();
        });
        await PopulateExtractedFilesListAsync();
    }

    /// Checks for duplicate extracted files in the ExtractedUnitypackages collection.
    /// Duplicates are removed from the SubdirectoryItems list of each ExtractedUnitypackageModel object.
    private void CheckForDuplicateExtractedFiles()
    {
        foreach (var unitypackage in ExtractedUnitypackages)
            unitypackage.SubdirectoryItems.RemoveAll(extractedFile =>
                ExtractedUnitypackages.All(x => x.UnitypackageName == extractedFile.FileName));
    }

    /// <summary>
    ///     Filters the extracted Unitypackages based on the search text.
    /// </summary>
    /// <param name="sender">The object that fired the event.</param>
    /// <param name="e">The event arguments.</param>
    private async void SearchBar_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(SearchBar.Text))
            {
                await UpdateExtractedFiles();
                return;
            }

            var filteredList = ExtractedUnitypackages.Where(x =>
                x.UnitypackageName.ToLower().Contains(SearchBar.Text.ToLower())).ToList();
            ExtractedUnitypackages.Clear();
            foreach (var unitypackage in filteredList) ExtractedUnitypackages.Add(unitypackage);
        });
    }

    /// <summary>
    ///     Deletes selected Unitypackages and their associated files.
    /// </summary>
    /// <param name="sender">The object that raised the event.</param>
    /// <param name="e">The event arguments.</param>
    private async void DeleteSelectedBtn_OnClick(object sender, RoutedEventArgs e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            // Delete selected Unitypackages
            var selectedUnitypackages = ExtractedUnitypackages.Where(x => x.PackageIsChecked).ToList();
            foreach (var unitypackage in selectedUnitypackages)
            {
                Directory.Delete(unitypackage.UnitypackagePath, true);
                ExtractedUnitypackages.Remove(unitypackage);
            }

            // Delete selected items within Unitypackages
            var selectedItems = ExtractedUnitypackages.SelectMany(x => x.SubdirectoryItems)
                .Where(x => x.IsChecked).ToList();
            foreach (var selectedItem in selectedItems)
            {
                File.Delete(selectedItem.FilePath);
                var parentUnitypackage = ExtractedUnitypackages.First(x => x.SubdirectoryItems.Contains(selectedItem));
                parentUnitypackage.SubdirectoryItems.Remove(selectedItem);
            }

            if (selectedItems.Count == 0 && selectedUnitypackages.Count == 0)
            {
                DeleteSelectedBtn.Content = "No Unitypackage or File selected";
                DeleteSelectedBtn.Appearance = ControlAppearance.Danger;
                DeleteSelectedBtn.Icon = new SymbolIcon(SymbolRegular.ErrorCircle24);

                await Task.Delay(3000); //wait for 3 seconds

                DeleteSelectedBtn.Content = "Delete Selected";
                DeleteSelectedBtn.Appearance = ControlAppearance.Secondary;
                DeleteSelectedBtn.Icon = new SymbolIcon(SymbolRegular.Delete24);
                return;
            }

            // Update UI
            Dispatcher.InvokeAsync(() =>
            {
                DeleteSelectedBtn.Content =
                    $"Deleted {selectedUnitypackages.Count} Unitypackages and {selectedItems.Count} Files";
                DeleteSelectedBtn.Appearance = ControlAppearance.Success;
                DeleteSelectedBtn.Icon = new SymbolIcon(SymbolRegular.Checkmark24);
                SelectAllUnitypackageToggle.IsChecked = false;
            });
            await Task.Delay(1000); //wait for a second
            Dispatcher.InvokeAsync(() =>
            {
                DeleteSelectedBtn.Content = "Delete Selected";
                DeleteSelectedBtn.Appearance = ControlAppearance.Secondary;
                DeleteSelectedBtn.Icon = new SymbolIcon(SymbolRegular.Delete24);
            });

            await UpdateQueueHeaderAsync();
            await UpdateInfoBadgesAsync();
            await UpdateExtractedFiles();
            UpdateSelectAllToggleContent();
        });
        await BetterLogger.LogAsync("Deleted Selected Unitypackages and Files", Importance.Info);
    }

    private async void IgnoreSelectedBtn_OnClick(object sender, RoutedEventArgs e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            // Ignore selected Unitypackage
            var selectedUnitypackages = ExtractedUnitypackages.Where(x => x.PackageIsChecked).ToList();
            foreach (var unitypackage in selectedUnitypackages)
            {
                await AddToIgnoredUnitypackagesAsync(new SearchEverythingModel
                {
                    UnityPackageName = unitypackage.UnitypackageName,
                    UnityPackagePath = unitypackage.UnitypackagePath
                }, "Manually ignored");
                ExtractedUnitypackages.Remove(unitypackage);
            }

            if (selectedUnitypackages.Count == 0)
            {
                IgnoreSelectedBtn.Content = "No Unitypackage selected";
                IgnoreSelectedBtn.Appearance = ControlAppearance.Danger;
                IgnoreSelectedBtn.Icon = new SymbolIcon(SymbolRegular.ErrorCircle24);

                await Task.Delay(3000); //wait for 3 seconds

                IgnoreSelectedBtn.Content = "Ignore Selected";
                IgnoreSelectedBtn.Appearance = ControlAppearance.Secondary;
                IgnoreSelectedBtn.Icon = new SymbolIcon(SymbolRegular.Delete24);
                return;
            }

            // Update UI
            IgnoreSelectedBtn.Content = $"Ignored {selectedUnitypackages.Count} Unitypackages";
            IgnoreSelectedBtn.Appearance = ControlAppearance.Success;
            IgnoreSelectedBtn.Icon = new SymbolIcon(SymbolRegular.Checkmark24);

            await Task.Delay(1000); //wait for a second

            IgnoreSelectedBtn.Content = "Ignore Selected";
            IgnoreSelectedBtn.Appearance = ControlAppearance.Secondary;
            IgnoreSelectedBtn.Icon = new SymbolIcon(SymbolRegular.Delete24);

            await UpdateQueueHeaderAsync();
            await UpdateInfoBadgesAsync();
            await UpdateExtractedFiles();
            UpdateSelectAllToggleContent();
        });
    }

    private async void OpenSelectedDirectoryBtn_OnClick(object sender, RoutedEventArgs e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            var selectedUnitypackage = ExtractedUnitypackages.FirstOrDefault(x => x.PackageIsChecked);

            #region UI Update

            if (selectedUnitypackage == null)
            {
                OpenSelectedDirectoryBtn.Content = "No Unitypackage selected";
                OpenSelectedDirectoryBtn.Appearance = ControlAppearance.Danger;
                OpenSelectedDirectoryBtn.Icon = new SymbolIcon(SymbolRegular.ErrorCircle24);

                await Task.Delay(3000); //wait for 3 seconds

                OpenSelectedDirectoryBtn.Content = "Open Selected Directory";
                OpenSelectedDirectoryBtn.Appearance = ControlAppearance.Secondary;
                OpenSelectedDirectoryBtn.Icon = new SymbolIcon(SymbolRegular.Folder24);
                return;
            }

            #endregion

            if (selectedUnitypackage != null)
            {
                var directoryToOpen = Path.GetFullPath(Path.GetDirectoryName(selectedUnitypackage.UnitypackagePath));
                if (directoryToOpen != null)
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = directoryToOpen,
                        UseShellExecute = true,
                        Verb = "open"
                    });
            }
        });
    }

    private async void MoveToDifferentDirectoryBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var standardString = "Move Selected";
        // only whole unitypackages
        var selectedUnitypackage = ExtractedUnitypackages.FirstOrDefault(x => x.PackageIsChecked);
        if (selectedUnitypackage == null)
        {
            MoveToDifferentDirectoryBtn.Content = "No Unitypackage selected";
            MoveToDifferentDirectoryBtn.Appearance = ControlAppearance.Danger;
            MoveToDifferentDirectoryBtn.Icon = new SymbolIcon(SymbolRegular.ErrorCircle24);

            await Task.Delay(3000); //wait for 3 seconds

            MoveToDifferentDirectoryBtn.Content = standardString;
            MoveToDifferentDirectoryBtn.Appearance = ControlAppearance.Secondary;
            MoveToDifferentDirectoryBtn.Icon = new SymbolIcon(SymbolRegular.Directions24);

            return;
        }

        if (ExtractedUnitypackages.Count(x => x.PackageIsChecked) > 1)
        {
            MoveToDifferentDirectoryBtn.Content = "Only 1 Unitypackage at a time";
            MoveToDifferentDirectoryBtn.Appearance = ControlAppearance.Danger;
            MoveToDifferentDirectoryBtn.Icon = new SymbolIcon(SymbolRegular.ErrorCircle24);

            await Task.Delay(3000); //wait for 3 seconds

            MoveToDifferentDirectoryBtn.Content = standardString;
            MoveToDifferentDirectoryBtn.Appearance = ControlAppearance.Secondary;
            MoveToDifferentDirectoryBtn.Icon = new SymbolIcon(SymbolRegular.Directions24);
            return;
        }

        var folderDialog = new OpenFolderDialog
        {
            Multiselect = false
        };

        try
        {
            if (folderDialog.ShowDialog() == true)
            {
                var newDirectory = folderDialog.FolderName;
                var newDirectoryPath = Path.Combine(newDirectory, selectedUnitypackage.UnitypackageName);
                Directory.Move(selectedUnitypackage.UnitypackagePath, newDirectoryPath);
                selectedUnitypackage.UnitypackagePath = newDirectoryPath;
            }

            // Update UI
            MoveToDifferentDirectoryBtn.Content =
                $"Moved {selectedUnitypackage.UnitypackageName} to different directory";
            MoveToDifferentDirectoryBtn.Appearance = ControlAppearance.Success;
            MoveToDifferentDirectoryBtn.Icon = new SymbolIcon(SymbolRegular.Checkmark24);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            MoveToDifferentDirectoryBtn.Content = "Failed to move Unitypackage";
            MoveToDifferentDirectoryBtn.Appearance = ControlAppearance.Danger;
            MoveToDifferentDirectoryBtn.Icon = new SymbolIcon(SymbolRegular.ErrorCircle24);

            await Task.Delay(3000); //wait for 3 seconds

            MoveToDifferentDirectoryBtn.Content = standardString;
            MoveToDifferentDirectoryBtn.Appearance = ControlAppearance.Secondary;
            MoveToDifferentDirectoryBtn.Icon = new SymbolIcon(SymbolRegular.Directions24);
        }

        await Task.Delay(1000); //wait for 1 second

        MoveToDifferentDirectoryBtn.Content = standardString;
        MoveToDifferentDirectoryBtn.Appearance = ControlAppearance.Secondary;
        MoveToDifferentDirectoryBtn.Icon = new SymbolIcon(SymbolRegular.Directions24);

        await UpdateQueueHeaderAsync();
        await UpdateInfoBadgesAsync();
        await UpdateExtractedFiles();
        UpdateSelectAllToggleContent();
    }

    private async void ClearIgnoredListBtn_OnClick(object sender, RoutedEventArgs e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            var ignoredUnitypackages = IgnoredUnitypackages.ToList();
            foreach (var ignoredUnitypackage in ignoredUnitypackages)
            {
                var ignoredAppDataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "EasyExtract", "IgnoredUnitypackages");
                var ignoredUnitypackagePath =
                    Path.Combine(ignoredAppDataDirectory, ignoredUnitypackage.IgnoredUnityPackageName);
                if (Directory.Exists(ignoredUnitypackagePath))
                    Directory.Delete(ignoredUnitypackagePath, true);
                IgnoredUnitypackages.Remove(ignoredUnitypackage);
            }

            if (ignoredUnitypackages.Count == 0)
            {
                ClearIgnoredListBtn.Content = "No Ignored Unitypackage to clear";
                ClearIgnoredListBtn.Appearance = ControlAppearance.Danger;
                ClearIgnoredListBtn.Icon = new SymbolIcon(SymbolRegular.ErrorCircle24);

                await Task.Delay(3000); //wait for 3 seconds

                ClearIgnoredListBtn.Content = "Clear Ignored List";
                ClearIgnoredListBtn.Appearance = ControlAppearance.Secondary;
                ClearIgnoredListBtn.Icon = new SymbolIcon(SymbolRegular.Delete24);
                return;
            }

            // Update UI
            ClearIgnoredListBtn.Content = $"Cleared {ignoredUnitypackages.Count} Ignored Unitypackages";
            ClearIgnoredListBtn.Appearance = ControlAppearance.Success;
            ClearIgnoredListBtn.Icon = new SymbolIcon(SymbolRegular.Checkmark24);

            await Task.Delay(1000); //wait for 1 second

            ClearIgnoredListBtn.Content = "Clear Ignored List";
            ClearIgnoredListBtn.Appearance = ControlAppearance.Secondary;
            ClearIgnoredListBtn.Icon = new SymbolIcon(SymbolRegular.Delete24);

            await UpdateQueueHeaderAsync();
            await UpdateInfoBadgesAsync();
            await UpdateExtractedFiles();
            UpdateSelectAllToggleContent();
        });
    }

    private void SelectAllUnitypackageToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        foreach (var unitypackage in ExtractedUnitypackages) unitypackage.PackageIsChecked = true;
    }

    private void SelectAllUnitypackageToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        foreach (var unitypackage in ExtractedUnitypackages) unitypackage.PackageIsChecked = false;
    }

    private void AnalyticsExpander_OnExpanded(object sender, RoutedEventArgs e)
    {
        UpdateChart();
    }

    private void Extraction_OnSizeChanged(object sender, SizeChangedEventArgs e)
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