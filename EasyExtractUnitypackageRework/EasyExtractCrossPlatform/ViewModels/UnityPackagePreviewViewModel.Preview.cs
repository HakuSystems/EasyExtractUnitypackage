using System.Runtime.InteropServices;
using System.Text;
using Docnet.Core;
using Docnet.Core.Models;
using StbImageSharp;

namespace EasyExtractCrossPlatform.ViewModels;

public sealed partial class UnityPackagePreviewViewModel
{
    // 8K x 8K RGBA is the largest texture the managed fallback decoder will inflate.
    private const long MaxManagedDecodePixels = 8192L * 8192L;
    private static readonly PageDimensions PdfPreviewDimensions = new(2.0d);
    private Bitmap? _fallbackPreviewImage;
    private int _previewTabIndex;
    private Bitmap? _primaryImagePreview;
    private UnityPackageAssetPreviewItem? _selectedAsset;

    public UnityPackageAssetPreviewItem? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (ReferenceEquals(_selectedAsset, value))
                return;

            _selectedAsset = value;
            OnPropertyChanged(nameof(SelectedAsset));
            OnPropertyChanged(nameof(SelectedAssetPath));
            OnPropertyChanged(nameof(SelectedAssetSizeText));
            OnPropertyChanged(nameof(SelectedAssetCategory));
            OnPropertyChanged(nameof(SelectedAssetFolder));
            OnPropertyChanged(nameof(IsAssetDataTruncated));
            UpdateSelectedPreviewContent();
            OnPropertyChanged(nameof(ShowUnsupportedModelMessage));
            OnPropertyChanged(nameof(ShowUnsupportedAudioMessage));

            if (!_suppressTreeSelectionSync)
                UpdateTreeSelectionFromAsset(_selectedAsset);
        }
    }

    public string? SelectedAssetPath => SelectedAsset?.RelativePath;

    public string? SelectedAssetSizeText => SelectedAsset?.SizeText;

    public string? SelectedAssetCategory => SelectedAsset?.Category;

    public string? SelectedAssetFolder => SelectedAsset?.Directory;

    public bool IsAssetDataTruncated => SelectedAsset?.IsAssetDataTruncated == true;

    public Bitmap? ImagePreview => _primaryImagePreview;

    public bool HasImagePreview => _primaryImagePreview is not null;

    public Bitmap? FallbackPreviewImage => _fallbackPreviewImage;

    public bool HasFallbackPreview => _fallbackPreviewImage is not null;

    public bool HasAnyImagePreview => HasImagePreview || HasFallbackPreview;

    public string? TextPreview { get; private set; }

    public bool HasTextPreview => !string.IsNullOrEmpty(TextPreview);

    public bool IsTextPreviewTruncated { get; private set; }

    public bool HasModelPreview => ModelPreview is not null;

    public ModelPreviewData? ModelPreview { get; private set; }

    public bool HasAnyPreview =>
        HasImagePreview || HasFallbackPreview || HasTextPreview || HasAudioPreview || HasModelPreview;

    public bool ShowUnsupportedModelMessage =>
        string.Equals(SelectedAsset?.Category, "3D Model", StringComparison.OrdinalIgnoreCase) && !HasModelPreview;

    public bool ShowUnsupportedAudioMessage =>
        string.Equals(SelectedAsset?.Category, "Audio", StringComparison.OrdinalIgnoreCase) && !HasAudioPreview;

    public int PreviewTabIndex
    {
        get => _previewTabIndex;
        set
        {
            if (_previewTabIndex == value)
                return;

            _previewTabIndex = value;
            OnPropertyChanged(nameof(PreviewTabIndex));
        }
    }

    private void UpdateSelectedPreviewContent()
    {
        var asset = SelectedAsset;
        var assetLabel = asset?.RelativePath ?? asset?.FileName ?? "<none>";
        LoggingService.LogInformation($"Updating preview content for '{assetLabel}'.");

        DisposeBitmap(ref _primaryImagePreview);
        DisposeBitmap(ref _fallbackPreviewImage);
        ResetAudioPreview();
        TextPreview = null;
        IsTextPreviewTruncated = false;
        ModelPreview = null;
        AudioStatusText = "Ready";
        AudioPositionText = FormatTime(TimeSpan.Zero);
        AudioDurationText = FormatTime(TimeSpan.Zero);
        AudioProgress = 0;

        if (asset is not null)
        {
            TryCreatePrimaryImagePreview(asset);
            TryCreateFallbackPreview(asset);
            TryCreateTextPreview(asset);
            TryCreateAudioPreview(asset);
            TryCreateModelPreview(asset);
        }

        LoggingService.LogInformation(
            $"Preview state for '{assetLabel}': HasImage={_primaryImagePreview is not null}, HasFallback={_fallbackPreviewImage is not null}, HasText={TextPreview is not null}, HasAudio={_audioPreviewSession is not null}, HasModel={ModelPreview is not null}.");

        RaisePreviewPropertyChanges();
        PreviewTabIndex = DetermineDefaultPreviewTabIndex();
        UpdateAudioCommands();
    }

    private void RaisePreviewPropertyChanges()
    {
        OnPropertyChanged(nameof(ImagePreview));
        OnPropertyChanged(nameof(HasImagePreview));
        OnPropertyChanged(nameof(FallbackPreviewImage));
        OnPropertyChanged(nameof(HasFallbackPreview));
        OnPropertyChanged(nameof(HasAnyImagePreview));
        OnPropertyChanged(nameof(TextPreview));
        OnPropertyChanged(nameof(HasTextPreview));
        OnPropertyChanged(nameof(IsTextPreviewTruncated));
        OnPropertyChanged(nameof(HasAudioPreview));
        OnPropertyChanged(nameof(IsAudioPlaying));
        OnPropertyChanged(nameof(CanSeekAudio));
        OnPropertyChanged(nameof(ModelPreview));
        OnPropertyChanged(nameof(HasModelPreview));
        OnPropertyChanged(nameof(HasAnyPreview));
        OnPropertyChanged(nameof(ShowUnsupportedModelMessage));
        OnPropertyChanged(nameof(ShowUnsupportedAudioMessage));
    }

    private void TryCreatePrimaryImagePreview(UnityPackageAssetPreviewItem asset)
    {
        if (_primaryImagePreview is not null)
            return;

        if (asset.AssetData is not { Length: > 0 } || asset.IsAssetDataTruncated)
            return;

        if (IsImageExtension(asset.Extension))
        {
            try
            {
                using var memoryStream = new MemoryStream(asset.AssetData);
                _primaryImagePreview = new Bitmap(memoryStream);
                LoggingService.LogInformation($"Primary image preview created for '{asset.RelativePath}'.");
            }
            catch
            {
                // Skia cannot decode formats like TGA, PSD or HDR; try the managed decoder
                // before treating the asset as preview-less.
                DisposeBitmap(ref _primaryImagePreview);
                _primaryImagePreview = TryCreateManagedBitmap(asset.AssetData);

                if (_primaryImagePreview is not null)
                    LoggingService.LogInformation(
                        $"Primary image preview created via managed decoder for '{asset.RelativePath}'.");
                else
                    LoggingService.LogWarning(
                        $"No decoder available for image preview '{asset.RelativePath}'. Falling back to the embedded thumbnail.");
            }

            return;
        }

        if (!IsPdfExtension(asset.Extension))
            return;

        _primaryImagePreview = TryCreatePdfBitmap(asset.AssetData);
        if (_primaryImagePreview is null)
        {
            DisposeBitmap(ref _primaryImagePreview);
            LoggingService.LogWarning($"Failed to render PDF preview for '{asset.RelativePath}'.");
        }
        else
        {
            LoggingService.LogInformation($"PDF preview generated for '{asset.RelativePath}'.");
        }
    }

    private static Bitmap? TryCreateManagedBitmap(byte[] imageData)
    {
        if (imageData.Length == 0)
            return null;

        try
        {
            var image = ImageResult.FromMemory(imageData, ColorComponents.RedGreenBlueAlpha);
            if (image?.Data is null || image.Width <= 0 || image.Height <= 0)
                return null;

            if ((long)image.Width * image.Height > MaxManagedDecodePixels)
                return null;

            var bitmap = new WriteableBitmap(
                new PixelSize(image.Width, image.Height),
                new Vector(96, 96),
                PixelFormat.Rgba8888,
                AlphaFormat.Unpremul);

            using (var buffer = bitmap.Lock())
            {
                var srcStride = image.Width * 4;
                var dstStride = buffer.RowBytes;
                var rows = Math.Min(image.Height, buffer.Size.Height);

                if (srcStride == dstStride)
                    Marshal.Copy(image.Data, 0, buffer.Address, srcStride * rows);
                else
                    for (var row = 0; row < rows; row++)
                    {
                        var srcOffset = row * srcStride;
                        var destPtr = buffer.Address + row * dstStride;
                        Marshal.Copy(image.Data, srcOffset, destPtr, Math.Min(srcStride, dstStride));
                    }
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? TryCreatePdfBitmap(byte[] pdfData)
    {
        if (pdfData.Length == 0)
            return null;

        try
        {
            using var document = DocLib.Instance.GetDocReader(pdfData, PdfPreviewDimensions);
            var pageCount = document.GetPageCount();
            if (pageCount <= 0)
                return null;

            using var page = document.GetPageReader(0);
            var width = page.GetPageWidth();
            var height = page.GetPageHeight();
            if (width <= 0 || height <= 0)
                return null;

            var pixelData = page.GetImage(RenderFlags.RenderAnnotations);
            if (pixelData is null || pixelData.Length == 0)
                return null;

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (var buffer = bitmap.Lock())
            {
                var srcStride = width * 4;
                var dstStride = buffer.RowBytes;
                var rows = Math.Min(height, buffer.Size.Height);

                if (srcStride == dstStride)
                    Marshal.Copy(pixelData, 0, buffer.Address, srcStride * rows);
                else
                    for (var row = 0; row < rows; row++)
                    {
                        var srcOffset = row * srcStride;
                        var destPtr = buffer.Address + row * dstStride;
                        Marshal.Copy(pixelData, srcOffset, destPtr, Math.Min(srcStride, dstStride));
                    }
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void TryCreateFallbackPreview(UnityPackageAssetPreviewItem asset)
    {
        if (_fallbackPreviewImage is not null)
            return;

        if (asset.PreviewImageData is not { Length: > 0 })
            return;

        try
        {
            using var memoryStream = new MemoryStream(asset.PreviewImageData);
            _fallbackPreviewImage = new Bitmap(memoryStream);
            LoggingService.LogInformation($"Fallback preview image created for '{asset.RelativePath}'.");
        }
        catch
        {
            DisposeBitmap(ref _fallbackPreviewImage);
            LoggingService.LogWarning($"Failed to create fallback preview for '{asset.RelativePath}'.");
        }
    }

    private void TryCreateTextPreview(UnityPackageAssetPreviewItem asset)
    {
        if (asset.AssetData is not { Length: > 0 })
            return;

        if (!IsTextCandidate(asset))
            return;

        const int maxPreviewBytes = 256 * 1024;
        var data = asset.AssetData;
        var sliceLength = Math.Min(data.Length, maxPreviewBytes);
        var buffer = sliceLength == data.Length ? data : data[..sliceLength];
        var (text, usedEncoding) = TryDecodeText(buffer);
        if (text is null)
            return;

        if (sliceLength < data.Length || asset.IsAssetDataTruncated)
        {
            text +=
                $"{Environment.NewLine}{Environment.NewLine}... Preview truncated (showing first {FormatFileSize(sliceLength)}).";
            IsTextPreviewTruncated = true;
        }
        else
        {
            IsTextPreviewTruncated = false;
        }

        if (usedEncoding is not null && !usedEncoding.Equals(Encoding.UTF8))
            text = $"// Encoding: {usedEncoding.EncodingName}{Environment.NewLine}{text}";

        TextPreview = text;
        LoggingService.LogInformation(
            $"Generated text preview for '{asset.RelativePath}' (length={text.Length}).");
    }

    private void TryCreateModelPreview(UnityPackageAssetPreviewItem asset)
    {
        if (IsModelExtension(asset.Extension))
        {
            ModelPreview = TryParseModelAsset(asset);
            LoggingService.LogInformation(
                ModelPreview is null
                    ? $"Failed to generate model preview for '{asset.RelativePath}'."
                    : $"Model preview generated for '{asset.RelativePath}'.");
            return;
        }

        if (string.Equals(asset.Extension, ".prefab", StringComparison.OrdinalIgnoreCase))
            TryCreatePrefabModelPreview(asset);
    }

    private void TryCreatePrefabModelPreview(UnityPackageAssetPreviewItem prefab)
    {
        if (prefab.AssetData is not { Length: > 0 })
            return;

        string yaml;
        try
        {
            yaml = Encoding.UTF8.GetString(prefab.AssetData);
        }
        catch
        {
            return;
        }

        var meshGuids = PrefabMeshReferenceParser.ExtractMeshGuids(yaml);
        if (meshGuids.Count == 0)
            return;

        var parsedMeshes = new List<ModelPreviewData>();
        foreach (var guid in meshGuids)
        {
            var meshAsset = _allAssets.FirstOrDefault(candidate =>
                string.Equals(candidate.AssetKey, guid, StringComparison.OrdinalIgnoreCase) &&
                IsModelExtension(candidate.Extension));

            if (meshAsset is null)
                continue;

            var parsed = TryParseModelAsset(meshAsset);
            if (parsed is not null)
                parsedMeshes.Add(parsed);
        }

        if (parsedMeshes.Count == 0)
        {
            LoggingService.LogInformation(
                $"Prefab '{prefab.RelativePath}' references {meshGuids.Count} mesh guid(s) but none could be resolved to a previewable model.");
            return;
        }

        ModelPreview = ModelPreviewParser.Combine(parsedMeshes);
        LoggingService.LogInformation(
            $"Prefab model preview generated for '{prefab.RelativePath}' from {parsedMeshes.Count} referenced mesh asset(s).");
    }

    private static ModelPreviewData? TryParseModelAsset(UnityPackageAssetPreviewItem asset)
    {
        var data = asset.AssetData is { Length: > 0 } && !asset.IsAssetDataTruncated
            ? asset.AssetData
            : null;

        return ModelPreviewParser.TryParse(data, asset.AssetFilePath, asset.Extension);
    }

    private int DetermineDefaultPreviewTabIndex()
    {
        if (HasImagePreview)
            return 0;
        if (HasFallbackPreview)
            return 1;
        if (HasTextPreview)
            return 2;
        if (HasAudioPreview)
            return 3;
        if (HasModelPreview)
            return 4;
        return 0;
    }

    private static void DisposeBitmap(ref Bitmap? bitmap)
    {
        bitmap?.Dispose();
        bitmap = null;
    }

    private static bool IsImageExtension(string extension)
    {
        return UnityAssetClassification.IsTextureExtension(extension);
    }

    private static bool IsPdfExtension(string extension)
    {
        return UnityAssetClassification.IsPdfExtension(extension);
    }

    private static bool IsModelExtension(string extension)
    {
        return UnityAssetClassification.IsModelExtension(extension);
    }

    private static bool IsTextExtension(string extension)
    {
        return extension switch
        {
            ".cs" or ".js" or ".boo" or ".shader" or ".cg" or ".cginc" or ".compute" or ".hlsl" or ".glsl"
                or ".shadergraph"
                or ".shadersubgraph" or ".txt" or ".json"
                or ".xml" or ".yaml" or ".yml" or ".asmdef" or ".prefab" or ".mat" or ".anim" or ".controller"
                or ".overridecontroller" or ".mask" or ".meta" or ".uxml" or ".uss" => true,
            _ => false
        };
    }

    private static bool IsTextCandidate(UnityPackageAssetPreviewItem asset)
    {
        if (IsTextExtension(asset.Extension))
            return true;

        if (asset.Category is "Script" or "Animation")
            return true;

        if (asset.AssetData is not { Length: > 0 })
            return false;

        return IsLikelyText(asset.AssetData);
    }

    private static bool IsLikelyText(byte[] data)
    {
        var length = Math.Min(data.Length, 4096);
        var nonPrintable = 0;
        for (var i = 0; i < length; i++)
        {
            var b = data[i];
            if (b == 0)
                return false;

            if (b < 9 || b > 13 && b < 32)
                nonPrintable++;
        }

        return nonPrintable / (double)length < 0.2;
    }

    private static (string? Text, Encoding? Encoding) TryDecodeText(byte[] data)
    {
        Encoding? encoding = null;
        string? text = null;

        var encodings = new[]
        {
            new UTF8Encoding(false, true), Encoding.Unicode, Encoding.BigEndianUnicode, Encoding.UTF32
        };

        foreach (var candidate in encodings)
            try
            {
                text = candidate.GetString(data);
                encoding = candidate;
                break;
            }
            catch
            {
                // Try next encoding
            }

        if (text is null)
            try
            {
                text = Encoding.GetEncoding("ISO-8859-1").GetString(data);
                encoding = Encoding.GetEncoding("ISO-8859-1");
            }
            catch
            {
                text = null;
                encoding = null;
            }

        return (text, encoding);
    }
}