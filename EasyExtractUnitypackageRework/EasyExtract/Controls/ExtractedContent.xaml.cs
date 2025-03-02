using System.Collections.ObjectModel;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;

namespace EasyExtract.Controls;

public partial class ExtractedContent
{
    private readonly ExtractionHelper _extractionHelper = new();

    private readonly bool _isCategoryView = true;


    private readonly Dictionary<string, BitmapImage> _previewCache = new();

    public ExtractedContent()
    {
        InitializeComponent();
        Loaded += ExtractedContent_Loaded;
        ExtractedTreeView.ItemsSource = ExtractedUnitypackages;
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
        if (!Directory.Exists(path))
            return;

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
            UnitypackageSize = await _extractionHelper.GetReadableFileSize(size),
            UnitypackageExtractedDate = Directory.GetCreationTime(dir),
            SubdirectoryItems = new List<ExtractedFiles>()
        };
    }

    private async Task AddSubItemsAsync(ExtractedUnitypackageModel pkg, string dir)
    {
        foreach (var file in new DirectoryInfo(dir).EnumerateFiles("*.*", SearchOption.AllDirectories))
        {
            if (file.Name.EndsWith(".EASYEXTRACTPREVIEW.png"))
                continue; // Do not delete, just skip (cached images)

            var previewPath = $"{file.FullName}.EASYEXTRACTPREVIEW.png";
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

            var extractedFile = new ExtractedFiles
            {
                FileName = file.Name,
                FilePath = file.FullName,
                Category = await _extractionHelper.GetCategoryByExtension(file.Extension),
                Extension = file.Extension,
                Size = await _extractionHelper.GetReadableFileSize(file.Length),
                ExtractedDate = file.CreationTime,
                SymbolIconImage = await _extractionHelper.GetSymbolByExtension(file.Extension),
                IsCodeFile = new[] { ".cs", ".json", ".shader", ".txt" }.Contains(file.Extension.ToLower()),
                PreviewImage = previewImage,
                SecurityWarning = await GetSecurityWarningAsync(file, pkg)
            };

            pkg.SubdirectoryItems.Add(extractedFile);
        }

        pkg.DetailsSeverity = pkg.IsDangerousPackage ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
    }


    private async Task<string> GetSecurityWarningAsync(FileInfo file, ExtractedUnitypackageModel pkg)
    {
        if (file.Extension.Equals(".dll"))
        {
            if (await _extractionHelper.IsEncryptedDll(file.FullName))
            {
                pkg.HasEncryptedDll = true;
                return "Encrypted DLL detected!";
            }
        }
        else if (new[] { ".cs", ".txt", ".json", ".shader" }.Contains(file.Extension.ToLower()))
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
        if (_previewCache.TryGetValue(file.FullName, out var cached))
            return cached;

        var previewPath = $"{file.FullName}.EASYEXTRACTPREVIEW.png";
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

        _previewCache[file.FullName] = previewImage;
        return previewImage;
    }

    private async void ClearCachedPreviews_OnClick(object sender, RoutedEventArgs e)
    {
        var previews = Directory.GetFiles(ConfigHandler.Instance.Config.LastExtractedPath, "*.EASYEXTRACTPREVIEW.png",
            SearchOption.AllDirectories);
        foreach (var preview in previews)
            File.Delete(preview);

        _previewCache.Clear(); // Clear memory cache as well.

        await DialogHelper.ShowInfoDialogAsync(null,
            "Cached preview images deleted. Images will now be generated temporarily in memory.",
            "Cache Cleared");

        await UpdateExtractedFilesAsync(); // Refresh UI immediately
    }


    private void EditSuspiciousLinkList_OnClick(object sender, RoutedEventArgs e)
    {
        var linkFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "suspicious_links.txt");

        if (!File.Exists(linkFilePath))
            File.WriteAllText(linkFilePath, "http://example.com\nhttps://discord.com/api/webhooks");

        Process.Start("notepad.exe", linkFilePath);
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
        ExtractedTreeView.ItemsSource = string.IsNullOrWhiteSpace(query)
            ? ExtractedUnitypackages
            : new ObservableCollection<ExtractedUnitypackageModel>(
                ExtractedUnitypackages.Where(p => p.UnitypackageName.ToLower().Contains(query) ||
                                                  p.SubdirectoryItems.Any(i => i.FileName.ToLower().Contains(query))));
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
            Process.Start("explorer.exe", Path.GetFullPath(selected.UnitypackagePath));
    }

    private void OpenFileInEditor_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && File.Exists(path))
            Process.Start("code", $"\"{path}\"");
    }
}