using System.Formats.Tar;
using System.IO.Compression;
using System.Text;

namespace EasyExtractCrossPlatform.Services;

public interface IUnityPackagePreviewService
{
    Task<UnityPackagePreviewResult> LoadPreviewAsync(
        string packagePath,
        CancellationToken cancellationToken = default);
}

public sealed class UnityPackagePreviewService : IUnityPackagePreviewService
{
    private const long MaxEmbeddedAssetBytes = 8 * 1024 * 1024; // 8 MB
    private const string TemporaryExtractionFolderPrefix = "EasyExtractPreviewAudio";
    private const string TemporaryPackageFolderPrefix = "EasyExtractPreviewPackage";
    private static readonly HashSet<char> InvalidFileNameCharacters = Path.GetInvalidFileNameChars().ToHashSet();

    private static readonly PathSegmentNormalization[] EmptySegmentNormalizations =
        Array.Empty<PathSegmentNormalization>();

    public async Task<UnityPackagePreviewResult> LoadPreviewAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        if (!File.Exists(packagePath))
            throw new FileNotFoundException("Unitypackage file was not found.", packagePath);

        LoggingService.LogInformation($"Loading preview for '{packagePath}'.");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await Task.Run(() => LoadPreviewInternal(packagePath, cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();
            LoggingService.LogInformation(
                $"Preview loaded for '{packagePath}' in {stopwatch.Elapsed.TotalMilliseconds:F0} ms. AssetCount={result.Assets.Count}.");
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            LoggingService.LogError($"Failed to load preview for '{packagePath}'.", ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private static UnityPackagePreviewResult LoadPreviewInternal(string packagePath,
        CancellationToken cancellationToken)
    {
        LoggingService.LogInformation($"Parsing unitypackage for preview: '{packagePath}'.");

        var resolvedPackage = ResolvePackagePath(packagePath);

        try
        {
            using var packageStream = File.OpenRead(resolvedPackage.ResolvedPath);
            using var gzipStream = new GZipInputStream(packageStream);
            using var tarReader = new TarInputStream(gzipStream, Encoding.UTF8);

            var assetStates = new Dictionary<string, UnityPackageAssetPreviewState>(StringComparer.OrdinalIgnoreCase);

            TarEntry? entry;
            while ((entry = tarReader.GetNextEntry()) is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsDirectory)
                    continue;

                var entryName = entry.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(entryName))
                    continue;

                var (assetKey, componentName) = SplitEntryName(entryName);
                if (string.IsNullOrWhiteSpace(assetKey) || string.IsNullOrWhiteSpace(componentName))
                    continue;

                if (!assetStates.TryGetValue(assetKey, out var state))
                {
                    state = new UnityPackageAssetPreviewState(assetKey);
                    assetStates[assetKey] = state;
                }

                switch (componentName)
                {
                    case "pathname":
                        using (var buffer = new MemoryStream())
                        {
                            tarReader.CopyTo(buffer);
                            var path = Encoding.UTF8.GetString(buffer.ToArray());
                            var normalization = NormalizeRelativePath(path);
                            state.RelativePath = normalization.NormalizedPath;
                            state.PathNormalization = normalization;
                        }

                        break;
                    case "asset":
                        state.AssetSizeBytes = Math.Max(0, entry.Size);
                        state.AssetFilePath = null;
                        state.NeedsAssetExtraction = false;

                        if (entry.Size >= 0 && entry.Size <= MaxEmbeddedAssetBytes)
                        {
                            using var assetBuffer = new MemoryStream();
                            tarReader.CopyTo(assetBuffer);
                            state.AssetData = assetBuffer.ToArray();
                            state.IsAssetDataTruncated = false;
                        }
                        else
                        {
                            state.AssetData = null;
                            state.IsAssetDataTruncated = true;
                            state.NeedsAssetExtraction = true;
                        }

                        break;
                    case "asset.meta":
                        state.HasMetaFile = true;
                        break;
                    case "preview.png":
                        using (var memoryStream = new MemoryStream())
                        {
                            tarReader.CopyTo(memoryStream);
                            state.PreviewImageData = memoryStream.ToArray();
                        }

                        break;
                }
            }

            var extractionRoot = ExtractLargeAudioAssets(resolvedPackage.ResolvedPath, assetStates, cancellationToken);
            if (!string.IsNullOrWhiteSpace(extractionRoot))
                LoggingService.LogInformation(
                    $"Extracted large audio assets to temporary directory '{extractionRoot}'.");

            var assets = new List<UnityPackagePreviewAsset>(assetStates.Count);
            var directoriesToPrune = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long totalAssetSize = 0;

            foreach (var state in assetStates.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(state.RelativePath))
                    continue;

                TrackCorruptedDirectories(state, directoriesToPrune);

                var assetSize = state.AssetSizeBytes;
                totalAssetSize += assetSize;

                assets.Add(new UnityPackagePreviewAsset(
                    state.RelativePath!,
                    assetSize,
                    state.HasMetaFile,
                    state.PreviewImageData,
                    state.AssetData,
                    state.IsAssetDataTruncated,
                    state.AssetFilePath));
            }

            assets.Sort(static (left, right) =>
                string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase));

            var fileInfo = new FileInfo(packagePath);
            var packageSizeBytes = resolvedPackage.PackageSizeBytes ?? (fileInfo.Exists ? fileInfo.Length : 0);
            var lastModifiedUtc = resolvedPackage.LastModifiedUtc ?? (fileInfo.Exists
                ? new DateTimeOffset(fileInfo.LastWriteTimeUtc)
                : null);

            LoggingService.LogInformation(
                $"Preview data assembled for '{packagePath}'. Assets={assets.Count}, totalAssetSize={totalAssetSize}, directoriesToPrune={directoriesToPrune.Count}.");

            return new UnityPackagePreviewResult(
                packagePath,
                resolvedPackage.PackageName,
                packageSizeBytes,
                lastModifiedUtc,
                totalAssetSize,
                assets,
                directoriesToPrune,
                extractionRoot);
        }
        finally
        {
            CleanupTemporaryPackage(resolvedPackage);
        }
    }

    private static string? ExtractLargeAudioAssets(
        string packagePath,
        IDictionary<string, UnityPackageAssetPreviewState> assetStates,
        CancellationToken cancellationToken)
    {
        var candidates = assetStates.Values
            .Where(state =>
                state.NeedsAssetExtraction &&
                !string.IsNullOrWhiteSpace(state.RelativePath) &&
                IsAudioExtension(Path.GetExtension(state.RelativePath)))
            .ToDictionary(state => state.AssetKey, state => state, StringComparer.OrdinalIgnoreCase);

        if (candidates.Count == 0)
            return null;

        string? extractionRoot = null;

        using var packageStream = File.OpenRead(packagePath);
        using var gzipStream = new GZipInputStream(packageStream);
        using var tarReader = new TarInputStream(gzipStream, Encoding.UTF8);

        TarEntry? entry;
        while ((entry = tarReader.GetNextEntry()) is not null && candidates.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.IsDirectory)
                continue;

            var entryName = entry.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entryName))
                continue;

            var (assetKey, componentName) = SplitEntryName(entryName);
            if (string.IsNullOrWhiteSpace(assetKey) || !string.Equals(componentName, "asset", StringComparison.Ordinal))
                continue;

            if (!candidates.TryGetValue(assetKey, out var state))
                continue;

            extractionRoot ??= CreateExtractionRoot();

            var extension = Path.GetExtension(state.RelativePath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".audio";

            var safeName = SanitizeFileName(state.AssetKey);
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = Guid.NewGuid().ToString("N");

            var targetPath = Path.Combine(extractionRoot, $"{safeName}{extension}");
            Directory.CreateDirectory(extractionRoot);

            using (var output = File.Create(targetPath))
            {
                tarReader.CopyTo(output);
            }

            state.AssetFilePath = targetPath;
            state.IsAssetDataTruncated = false;
            state.NeedsAssetExtraction = false;

            candidates.Remove(assetKey);
        }

        if (candidates.Count > 0 && extractionRoot is not null && Directory.Exists(extractionRoot))
            try
            {
                Directory.Delete(extractionRoot, true);
                extractionRoot = null;
            }
            catch
            {
                // Ignore cleanup failure; remaining assets will continue to report truncated data.
            }

        return extractionRoot;
    }

    private static string CreateExtractionRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"{TemporaryExtractionFolderPrefix}_{Guid.NewGuid():N}");
        return root;
    }

    private static PackagePathResolution ResolvePackagePath(string packagePath)
    {
        var format = DetectPackageFormat(packagePath);
        return format switch
        {
            PackageFormat.GzipTar => new PackagePathResolution(packagePath, Path.GetFileName(packagePath), null, null,
                null),
            PackageFormat.Zip => ResolveFromZip(packagePath),
            _ => throw new InvalidDataException(
                "Unitypackage is not a valid gzip tar archive. If this came from a .zip, unpack the .unitypackage first.")
        };
    }

    private static PackagePathResolution ResolveFromZip(string packagePath)
    {
        LoggingService.LogInformation(
            $"Package '{packagePath}' appears to be a zip archive. Extracting embedded unitypackage.");

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"{TemporaryPackageFolderPrefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            using var zipFile = new ZipFile(packagePath);
            ZipEntry? targetEntry = null;

            foreach (ZipEntry entry in zipFile)
            {
                if (!entry.IsFile)
                    continue;

                var name = entry.Name ?? string.Empty;
                if (name.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                {
                    targetEntry = entry;
                    break;
                }
            }

            if (targetEntry is null)
                throw new InvalidDataException("Zip archive does not contain a .unitypackage payload.");

            var fileName = Path.GetFileName(targetEntry.Name);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "package.unitypackage";

            var targetPath = Path.Combine(tempDirectory, fileName);
            using (var entryStream = zipFile.GetInputStream(targetEntry))
            using (var output = File.Create(targetPath))
            {
                entryStream.CopyTo(output);
            }

            long? size = targetEntry.Size >= 0 ? targetEntry.Size : null;
            var lastModified = new DateTimeOffset(targetEntry.DateTime.ToUniversalTime());

            LoggingService.LogInformation($"Extracted '{fileName}' from zip '{packagePath}' for preview.");
            return new PackagePathResolution(targetPath, fileName, tempDirectory, size, lastModified);
        }
        catch
        {
            CleanupTemporaryDirectory(tempDirectory);
            throw;
        }
    }

    private static PackageFormat DetectPackageFormat(string packagePath)
    {
        using var stream = File.OpenRead(packagePath);
        Span<byte> header = stackalloc byte[4];
        var bytesRead = stream.Read(header);

        if (bytesRead >= 2 && header[0] == 0x1F && header[1] == 0x8B)
            return PackageFormat.GzipTar;

        if (bytesRead >= 2 && header[0] == 0x50 && header[1] == 0x4B)
            return PackageFormat.Zip;

        return PackageFormat.Unknown;
    }

    private static void CleanupTemporaryPackage(PackagePathResolution resolution)
    {
        if (resolution.TemporaryDirectory is null)
            return;

        CleanupTemporaryDirectory(resolution.TemporaryDirectory);
    }

    private static void CleanupTemporaryDirectory(string path)
    {
        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
        }
    }

    private static string SanitizeFileName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var filtered = new string(input.Where(c => !InvalidFileNameCharacters.Contains(c)).ToArray());
        return filtered.Trim();
    }

    private static bool IsAudioExtension(string? extension)
    {
        return UnityAssetClassification.IsAudioExtension(extension);
    }

    private static (string AssetKey, string ComponentName) SplitEntryName(string entryName)
    {
        var normalized = entryName.Replace('\\', '/');
        var firstSlash = normalized.IndexOf('/');
        if (firstSlash < 0)
            return (string.Empty, string.Empty);

        var key = normalized[..firstSlash].Trim();
        var remainder = normalized[(firstSlash + 1)..].Trim();
        return (key, remainder.ToLowerInvariant());
    }

    private static PathNormalizationResult NormalizeRelativePath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);

        var sanitized = input.Replace('\\', '/')
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);

        var segments = sanitized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);

        var originalSegments = new List<string>(segments.Length);
        var normalizedSegments = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
                continue;

            var trimmed = segment.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var filtered = new string(trimmed.Where(c => !InvalidFileNameCharacters.Contains(c)).ToArray());
            filtered = filtered.Trim();
            if (string.IsNullOrWhiteSpace(filtered))
                continue;

            originalSegments.Add(filtered);
            var normalizedSegment = FileExtensionNormalizer.Normalize(filtered);
            normalizedSegments.Add(normalizedSegment);
        }

        if (originalSegments.Count == 0)
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);

        var segmentPairs = new PathSegmentNormalization[originalSegments.Count];
        for (var i = 0; i < segmentPairs.Length; i++)
            segmentPairs[i] = new PathSegmentNormalization(originalSegments[i], normalizedSegments[i]);

        var originalPath = Path.Combine(originalSegments.ToArray());
        var normalizedPath = Path.Combine(normalizedSegments.ToArray());

        return new PathNormalizationResult(normalizedPath, originalPath, segmentPairs);
    }

    private static void TrackCorruptedDirectories(
        UnityPackageAssetPreviewState state,
        HashSet<string> directoriesToPrune)
    {
        if (state.PathNormalization is null)
            return;

        var normalization = state.PathNormalization.Value;
        var segments = normalization.Segments;
        if (segments.Count == 0)
            return;

        var builder = new StringBuilder();
        for (var i = 0; i < segments.Count - 1; i++)
        {
            if (builder.Length > 0)
                builder.Append('/');

            builder.Append(segments[i].Normalized);

            if (string.Equals(segments[i].Original, segments[i].Normalized, StringComparison.Ordinal))
                continue;

            directoriesToPrune.Add(builder.ToString());
        }
    }

    private static void CopyStreamWithCancellation(Stream source, Stream destination, CancellationToken token)
    {
        var buffer = new byte[81920];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();
            destination.Write(buffer, 0, read);
        }
    }

    private readonly record struct PathSegmentNormalization(string Original, string Normalized);

    private readonly record struct PathNormalizationResult(
        string NormalizedPath,
        string OriginalPath,
        IReadOnlyList<PathSegmentNormalization> Segments);

    private enum PackageFormat
    {
        Unknown,
        GzipTar,
        Zip
    }

    private readonly record struct PackagePathResolution(
        string ResolvedPath,
        string PackageName,
        string? TemporaryDirectory,
        long? PackageSizeBytes,
        DateTimeOffset? LastModifiedUtc);

    private sealed class UnityPackageAssetPreviewState
    {
        public UnityPackageAssetPreviewState(string assetKey)
        {
            AssetKey = assetKey;
        }

        public string AssetKey { get; }
        public string? RelativePath { get; set; }
        public PathNormalizationResult? PathNormalization { get; set; }
        public long AssetSizeBytes { get; set; }
        public bool HasMetaFile { get; set; }
        public byte[]? PreviewImageData { get; set; }
        public byte[]? AssetData { get; set; }
        public bool IsAssetDataTruncated { get; set; }
        public bool NeedsAssetExtraction { get; set; }
        public string? AssetFilePath { get; set; }
    }
}