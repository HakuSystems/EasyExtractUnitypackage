using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;
using EasyExtract.Views;
using Wpf.Ui.Controls;
using static System.Windows.Application;
using Button = System.Windows.Controls.Button;

namespace EasyExtract.Controls;

public partial class ExtractedContent
{
    private readonly ICollectionView _extractedPackagesView;
    private readonly ExtractionHelper _extractionHelper = new();


    private readonly Dictionary<string, BitmapImage> _previewCache = new();

    public ExtractedContent()
    {
        InitializeComponent();
        Loaded += ExtractedContent_Loaded;
        _extractedPackagesView = CollectionViewSource.GetDefaultView(ExtractedUnitypackages);
        ExtractedTreeView.ItemsSource = _extractedPackagesView;
    }

    public ObservableCollection<ExtractedUnitypackageModel> ExtractedUnitypackages { get; } = new();

    private async void ExtractedContent_Loaded(object sender, RoutedEventArgs e)
    {
        Dashboard.Instance.NavigateBackBtn.Visibility = Visibility.Visible;

        await DiscordRpcManager.Instance.TryUpdatePresenceAsync("Extracted Content");
        await UpdateExtractedFilesAsync();
    }


    private async Task UpdateExtractedFilesAsync()
    {
        ExtractedUnitypackages.Clear();
        var path = ConfigHandler.Instance.Config.LastExtractedPath;

        if (!Directory.Exists(path)) return;

        var dirs = Directory.GetDirectories(path);
        var tasks = dirs.Select(async dir =>
        {
            var pkg = await CreateUnitypackageModelAsync(dir);
            await AddSubItemsAsync(pkg, dir);
            return pkg;
        }).ToList();

        var pkgs = await Task.WhenAll(tasks);

        Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var pkg in pkgs)
                ExtractedUnitypackages.Add(pkg);
        });
    }


    private Task<ExtractedUnitypackageModel> CreateUnitypackageModelAsync(string dir)
    {
        var size = new DirectoryInfo(dir).EnumerateFiles("*.*", SearchOption.AllDirectories).Sum(f => f.Length);
        return Task.FromResult(new ExtractedUnitypackageModel
        {
            UnitypackageName = Path.GetFileName(dir),
            UnitypackagePath = dir,
            UnitypackageSize = new FileSizeConverter().Convert(size, null, null, CultureInfo.CurrentCulture).ToString(),
            UnitypackageExtractedDate = Directory.GetCreationTime(dir)
        });
    }

    private async Task AddSubItemsAsync(ExtractedUnitypackageModel pkg, string dir)
    {
        var files = new DirectoryInfo(dir)
            .EnumerateFiles("*.*", SearchOption.AllDirectories)
            .Where(f => !f.Name.EndsWith(".EASYEXTRACTPREVIEW.png"))
            .ToList();

        var subItemTasks = files.Select(async file =>
        {
            var previewTask = GetOrCreatePreviewAsync(file);
            var securityWarningTask = GetSecurityWarningAsync(file, pkg);

            await Task.WhenAll(previewTask, securityWarningTask);

            return new ExtractedFiles
            {
                FileName = file.Name,
                FilePath = file.FullName,
                Category = await _extractionHelper.GetCategoryByExtension(file.Extension),
                Extension = file.Extension,
                Size = new FileSizeConverter().Convert(file.Length, null, null, CultureInfo.CurrentCulture).ToString(),
                ExtractedDate = file.CreationTime,
                SymbolIconImage = await _extractionHelper.GetSymbolByExtension(file.Extension),
                IsCodeFile = new[] { ".cs", ".json", ".shader", ".txt" }.Contains(file.Extension.ToLower()),
                PreviewImage = previewTask.Result,
                SecurityWarning = securityWarningTask.Result
            };
        });

        var subItems = await Task.WhenAll(subItemTasks);

        Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var item in subItems)
                pkg.SubdirectoryItems.Add(item);

            pkg.DetailsSeverity = pkg.IsDangerousPackage
                ? InfoBarSeverity.Warning
                : InfoBarSeverity.Success;
        });
    }

    private static string GetVsCodeExecutablePath()
    {
        var possiblePaths = new[]
        {
            Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Microsoft VS Code\Code.exe"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft VS Code\Code.exe")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? string.Empty;
    }


    private async Task<string> GetSecurityWarningAsync(FileInfo file, ExtractedUnitypackageModel pkg)
    {
        if (file.Extension.Equals(".dll") && await _extractionHelper.IsEncryptedDll(file.FullName))
        {
            pkg.HasEncryptedDll = true;
            return "Encrypted DLL detected!";
        }

        var suspiciousExtensions = new[] { ".cs", ".txt", ".json", ".shader" };
        if (suspiciousExtensions.Contains(file.Extension.ToLower()))
        {
            var content = await File.ReadAllTextAsync(file.FullName);

            if (await MaliciousCodeDetector.StartDiscordWebhookScanAsync(content))
            {
                pkg.MalicousDiscordWebhookCount++;
                return "Discord webhook detected!";
            }

            if (await MaliciousCodeDetector.StartLinkDetectionAsync(content))
            {
                pkg.LinkDetectionCount++;
                return "Suspicious links detected!";
            }
        }

        return string.Empty;
    }

    private async Task<BitmapImage?> GetOrCreatePreviewAsync(FileInfo file)
    {
        var previewPath = $"{file.FullName}.EASYEXTRACTPREVIEW.png";
        if (_previewCache.TryGetValue(previewPath, out var cached))
            return cached;

        BitmapImage? previewImage;
        if (File.Exists(previewPath))
        {
            previewImage = await LoadImageWithoutLockAsync(previewPath);
        }
        else
        {
            previewImage = await GeneratePreviewImageAsync(file);
            if (previewImage != null)
                await Task.Run(() => CodeToImageConverter.SaveImageToFile(previewImage, previewPath));
        }

        if (previewImage != null)
            _previewCache[previewPath] = previewImage;

        return previewImage;
    }


    private void EditAllowedLinkList_OnClick(object sender, RoutedEventArgs e)
    {
        var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract");
        Directory.CreateDirectory(appDataFolder);
        var linkFilePath = Path.Combine(appDataFolder, "allowed_links.txt");

        if (!File.Exists(linkFilePath))
        {
            var documentation = "# Allowed Links List\n" +
                                "# Add links below (one per line). Lines starting with '#' are comments and ignored.\n" +
                                "http://example.com\n" +
                                "https://discord.com/api/webhooks";
            File.WriteAllText(linkFilePath, documentation);
        }

        Process.Start(new ProcessStartInfo("notepad.exe", linkFilePath) { UseShellExecute = true });
    }


    private static async Task<BitmapImage?> GeneratePreviewImageAsync(FileInfo file)
    {
        var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".psd" };
        if (imageExtensions.Contains(file.Extension.ToLower())) return await LoadImageWithoutLockAsync(file.FullName);

        var codeExtensions = new[] { ".cs", ".txt", ".json", ".shader" };
        if (codeExtensions.Contains(file.Extension.ToLower()))
        {
            var code = await File.ReadAllTextAsync(file.FullName);
            return CodeToImageConverter.ConvertCodeToImage(code);
        }

        return null;
    }

    private static async Task<BitmapImage?> LoadImageWithoutLockAsync(string filePath)
    {
        try
        {
            var bitmap = new BitmapImage();
            await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Failed to load image '{filePath}': {ex.Message}", Importance.Error);
            return null;
        }
    }


    private void RefreshExtractedButton_OnClick(object sender, RoutedEventArgs e)
    {
        Task.Run(async () =>
        {
            _previewCache.Clear();
            await UpdateExtractedFilesAsync();
        });
    }


    private void ExtractedSearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = ExtractedSearchBox.Text.ToLowerInvariant();

        _extractedPackagesView.Filter = item =>
        {
            if (item is ExtractedUnitypackageModel pkg)
                return pkg.UnitypackageName.Contains(query, StringComparison.OrdinalIgnoreCase)
                       || pkg.SubdirectoryItems.Any(i =>
                           i.FileName.Contains(query, StringComparison.OrdinalIgnoreCase));
            return false;
        };

        _extractedPackagesView.Refresh();
    }

    private async void DeleteExtractedButton_OnClick(object sender, RoutedEventArgs e)
    {
        var packagesToDelete = ExtractedUnitypackages.Where(pkg => pkg.PackageIsChecked).ToList();

        if (packagesToDelete.Any())
            foreach (var pkg in packagesToDelete)
                try
                {
                    Directory.Delete(pkg.UnitypackagePath, true);
                    ClearPreviewsFromCache(pkg.UnitypackagePath);
                    await Current.Dispatcher.InvokeAsync(() => ExtractedUnitypackages.Remove(pkg));
                }
                catch (Exception ex)
                {
                    await BetterLogger.LogAsync($"Error deleting package {pkg.UnitypackageName}: {ex.Message}",
                        Importance.Error);
                }
        else
            foreach (var extractedPkg in ExtractedUnitypackages.ToList())
            {
                var filesToDelete = extractedPkg.SubdirectoryItems.Where(file => file.IsChecked).ToList();

                foreach (var file in filesToDelete)
                    try
                    {
                        File.Delete(file.FilePath);
                        extractedPkg.SubdirectoryItems.Remove(file);
                    }
                    catch (Exception ex)
                    {
                        await BetterLogger.LogAsync($"Error deleting file {file.FileName}: {ex.Message}",
                            Importance.Error);
                    }

                await RecheckSecurityStatusAsync(extractedPkg);
            }

        UpdateConfigAndUI();

        async Task RecheckSecurityStatusAsync(ExtractedUnitypackageModel pkg)
        {
            pkg.HasEncryptedDll = false;
            pkg.MalicousDiscordWebhookCount = 0;
            pkg.LinkDetectionCount = 0;

            foreach (var file in pkg.SubdirectoryItems)
                file.SecurityWarning = await GetSecurityWarningAsync(new FileInfo(file.FilePath), pkg);

            pkg.DetailsSeverity = pkg.IsDangerousPackage ? InfoBarSeverity.Warning : InfoBarSeverity.Success;

            await Current.Dispatcher.InvokeAsync(() =>
            {
                pkg.OnPropertyChanged(nameof(pkg.PackageSecuritySummary));
                pkg.OnPropertyChanged(nameof(pkg.DetailsSeverity));
            });
        }

        void ClearPreviewsFromCache(string directoryPath)
        {
            var keysToRemove = _previewCache.Keys
                .Where(key => key.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove) _previewCache.Remove(key);
        }

        void UpdateConfigAndUI()
        {
            try
            {
                ConfigHandler.Instance.Config.TotalSizeBytes = ExtractedUnitypackages
                    .Sum(extractedPackage => new DirectoryInfo(extractedPackage.UnitypackagePath)
                        .EnumerateFiles("*", SearchOption.AllDirectories)
                        .Sum(file => file.Length));

                ConfigHandler.Instance.Config.ExtractedUnitypackages =
                    new ObservableCollection<ExtractedUnitypackageModel>(ExtractedUnitypackages);
                ConfigHandler.Instance.OverrideConfig();
                Current.Dispatcher.InvokeAsync(() => _extractedPackagesView.Refresh());
            }
            catch (Exception ex)
            {
                BetterLogger.LogAsync($"Error updating configuration: {ex.Message}", Importance.Error).Wait();
            }
        }
    }

    private void OpenExtractedDirectoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = ExtractedUnitypackages.FirstOrDefault(x => x.PackageIsChecked);
        if (selected != null)
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{selected.UnitypackagePath}\"")
                { UseShellExecute = true });
    }

    private void OpenFileInEditor_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && File.Exists(path))
        {
            var codePath = GetVsCodeExecutablePath();
            if (string.IsNullOrEmpty(codePath)) return;

            Process.Start(new ProcessStartInfo(codePath, path) { UseShellExecute = true });
        }
    }

    private void ExtractedTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ExtractedUnitypackageModel selectedPkg)
            selectedPkg.PackageIsChecked = true;
    }
}