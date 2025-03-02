using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;

namespace EasyExtract.Controls;

public partial class ExtractedContent
{
    private readonly ICollectionView _extractedPackagesView;
    private readonly ExtractionHelper _extractionHelper = new();

    private readonly bool _isCategoryView = true;


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
        await DiscordRpcManager.Instance.TryUpdatePresenceAsync("Extracted Content");
        await UpdateExtractedFilesAsync();
    }


    private async Task UpdateExtractedFilesAsync()
    {
        ExtractedUnitypackages.Clear();
        var path = ConfigHandler.Instance.Config.LastExtractedPath;
        if (!Directory.Exists(path)) return;

        foreach (var dir in Directory.GetDirectories(path))
        {
            var pkg = await CreateUnitypackageModelAsync(dir);
            await AddSubItemsAsync(pkg, dir);
            ExtractedUnitypackages.Add(pkg);
        }
    }


    private async Task<ExtractedUnitypackageModel> CreateUnitypackageModelAsync(string dir)
    {
        var size = new DirectoryInfo(dir).EnumerateFiles("*.*", SearchOption.AllDirectories).Sum(f => f.Length);
        return new ExtractedUnitypackageModel
        {
            UnitypackageName = Path.GetFileName(dir),
            UnitypackagePath = dir,
            UnitypackageSize = new FileSizeConverter().Convert(size, null, null, CultureInfo.CurrentCulture).ToString(),
            UnitypackageExtractedDate = Directory.GetCreationTime(dir)
        };
    }

    private async Task AddSubItemsAsync(ExtractedUnitypackageModel pkg, string dir)
    {
        foreach (var file in new DirectoryInfo(dir).EnumerateFiles("*.*", SearchOption.AllDirectories))
        {
            if (file.Name.EndsWith(".EASYEXTRACTPREVIEW.png")) continue;

            var previewImage = await GetOrCreatePreviewAsync(file);
            pkg.SubdirectoryItems.Add(new ExtractedFiles
            {
                FileName = file.Name,
                FilePath = file.FullName,
                Category = await _extractionHelper.GetCategoryByExtension(file.Extension),
                Extension = file.Extension,
                Size = new FileSizeConverter().Convert(file.Length, null, null, CultureInfo.CurrentCulture).ToString(),
                ExtractedDate = file.CreationTime,
                SymbolIconImage = await _extractionHelper.GetSymbolByExtension(file.Extension),
                IsCodeFile = new[] { ".cs", ".json", ".shader", ".txt" }.Contains(file.Extension.ToLower()),
                PreviewImage = previewImage,
                SecurityWarning = await GetSecurityWarningAsync(file, pkg)
            });
        }

        pkg.DetailsSeverity = pkg.IsDangerousPackage ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
    }

    private string GetVSCodeExecutablePath()
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
        if (_previewCache.TryGetValue(previewPath, out var cached)) return cached;

        BitmapImage previewImage;
        if (File.Exists(previewPath))
        {
            previewImage = new BitmapImage(new Uri(previewPath));
        }
        else
        {
            previewImage = await GeneratePreviewImageAsync(file);
            if (previewImage != null)
                CodeToImageConverter.SaveImageToFile(previewImage, previewPath);
        }

        _previewCache[previewPath] = previewImage;
        return previewImage;
    }

    private async void ClearCachedPreviews_OnClick(object sender, RoutedEventArgs e)
    {
        var previews = Directory.GetFiles(ConfigHandler.Instance.Config.LastExtractedPath, "*.EASYEXTRACTPREVIEW.png",
            SearchOption.AllDirectories);
        foreach (var preview in previews)
            try
            {
                File.Delete(preview);
            }
            catch (Exception ex)
            {
                await BetterLogger.LogAsync(ex.ToString(), Importance.Error);
            }

        _previewCache.Clear();

        await UpdateExtractedFilesAsync();
    }


    private void EditSuspiciousLinkList_OnClick(object sender, RoutedEventArgs e)
    {
        var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract");
        Directory.CreateDirectory(appDataFolder);
        var linkFilePath = Path.Combine(appDataFolder, "suspicious_links.txt");

        if (!File.Exists(linkFilePath))
            File.WriteAllText(linkFilePath, "http://example.com\nhttps://discord.com/api/webhooks");

        Process.Start(new ProcessStartInfo("notepad.exe", linkFilePath) { UseShellExecute = true });
    }


    private async Task<BitmapImage?> GeneratePreviewImageAsync(FileInfo file)
    {
        var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".psd" };
        if (imageExtensions.Contains(file.Extension.ToLower()))
            return new BitmapImage(new Uri(file.FullName));

        var codeExtensions = new[] { ".cs", ".txt", ".json", ".shader" };
        if (codeExtensions.Contains(file.Extension.ToLower()))
        {
            var code = await File.ReadAllTextAsync(file.FullName);
            return CodeToImageConverter.ConvertCodeToImage(code);
        }

        return null;
    }


    private async void RefreshExtractedButton_OnClick(object sender, RoutedEventArgs e)
    {
        await UpdateExtractedFilesAsync();
    }

    private void ExtractedSearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = ExtractedSearchBox.Text.ToLower();

        _extractedPackagesView.Filter = item =>
        {
            if (item is ExtractedUnitypackageModel pkg)
                return pkg.UnitypackageName.ToLower().Contains(query) ||
                       pkg.SubdirectoryItems.Any(i => i.FileName.ToLower().Contains(query));
            return false;
        };

        _extractedPackagesView.Refresh();
    }

    private void DeleteExtractedButton_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var pkg in ExtractedUnitypackages.Where(x => x.PackageIsChecked).ToList())
        {
            Directory.Delete(pkg.UnitypackagePath, true);
            ExtractedUnitypackages.Remove(pkg);
        }
    }

    private void OpenExtractedDirectoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = ExtractedUnitypackages.FirstOrDefault(x => x.PackageIsChecked);
        if (selected != null)
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{selected.UnitypackagePath}\"")
                { UseShellExecute = true });
    }

    private void OpenFileInEditor_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && File.Exists(path))
        {
            var codePath = GetVSCodeExecutablePath();
            if (string.IsNullOrEmpty(codePath)) return;

            Process.Start(codePath, $"\"{path}\"");
        }
    }

    private void ExtractedTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ExtractedUnitypackageModel selectedPkg)
            selectedPkg.PackageIsChecked = true;
    }
}