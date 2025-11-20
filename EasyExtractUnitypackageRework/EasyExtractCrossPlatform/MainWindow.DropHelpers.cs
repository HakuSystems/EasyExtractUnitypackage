namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private static (List<string> ValidPaths, bool DetectedUnityPackage) ResolveUnityPackagePaths(DragEventArgs e)
    {
        var validPaths = new List<string>();
        var detectedUnityPackage = false;
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in EnumerateDroppedItemNames(e))
        {
            var trimmedCandidate = candidate?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedCandidate))
                continue;

            if (IsUnityPackage(trimmedCandidate))
                detectedUnityPackage = true;

            if (!TryResolveDroppedPath(trimmedCandidate, out var resolvedPath))
                continue;

            if (!IsUnityPackage(resolvedPath))
                continue;

            try
            {
                var normalizedPath = Path.GetFullPath(resolvedPath);
                if (!File.Exists(normalizedPath))
                    continue;

                if (uniquePaths.Add(normalizedPath))
                    validPaths.Add(normalizedPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to normalize dropped path '{resolvedPath}': {ex}");
            }
        }

        return (validPaths, detectedUnityPackage);
    }

    private static bool ContainsUnityPackage(DragEventArgs e)
    {
        foreach (var candidate in EnumerateDroppedItemNames(e))
            if (IsUnityPackage(candidate))
                return true;

        return false;
    }

    private static IEnumerable<string> EnumerateDroppedItemNames(DragEventArgs e)
    {
        var dataObject = e.Data;
        if (dataObject is null)
            yield break;

        List<IStorageItem>? storageItems = null;

        try
        {
            var files = dataObject.GetFiles();
            if (files is not null)
                storageItems = files.ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enumerate dropped storage items: {ex}");
        }

        if (storageItems is not null)
            foreach (var item in storageItems)
            {
                if (item is null)
                    continue;

                foreach (var candidate in EnumerateStorageItemEntries(item))
                    yield return candidate;
            }

        if (dataObject.Contains(DataFormats.FileNames))
        {
            var rawFileNames = dataObject.Get(DataFormats.FileNames);
            switch (rawFileNames)
            {
                case IEnumerable<string> names:
                    foreach (var name in names)
                        if (!string.IsNullOrWhiteSpace(name))
                            yield return name;
                    break;
                case string singleName when !string.IsNullOrWhiteSpace(singleName):
                    yield return singleName;
                    break;
            }
        }

        if (dataObject.Contains(DataFormats.Text))
        {
            var textData = dataObject.Get(DataFormats.Text);
            foreach (var entry in EnumerateTextDataEntries(textData))
                if (!string.IsNullOrWhiteSpace(entry))
                    yield return entry;
        }
    }

    private static IEnumerable<string> EnumerateStorageItemEntries(IStorageItem item)
    {
        string? localPath = null;
        try
        {
            localPath = item.TryGetLocalPath();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to resolve local path for dropped item: {ex}");
        }

        if (!string.IsNullOrWhiteSpace(localPath))
            yield return localPath;

        Uri? itemUri = null;
        try
        {
            itemUri = item.Path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to resolve URI for dropped item: {ex}");
        }

        if (itemUri is { IsAbsoluteUri: true })
        {
            if (itemUri.IsFile && !string.IsNullOrWhiteSpace(itemUri.LocalPath))
                yield return itemUri.LocalPath;
            else
                yield return itemUri.AbsoluteUri;
        }

        if (item is IStorageFile file && !string.IsNullOrWhiteSpace(file.Name))
            yield return file.Name;
    }

    private static bool TryResolveDroppedPath(string candidate, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var input = candidate.Trim();

        if (Uri.TryCreate(input, UriKind.Absolute, out var absoluteUri))
        {
            if (!absoluteUri.IsFile)
                return false;

            input = absoluteUri.LocalPath;
        }

        if (!Path.IsPathRooted(input))
            return false;

        try
        {
            resolvedPath = Path.GetFullPath(input);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to resolve dropped path '{candidate}': {ex}");
            return false;
        }
    }

    private static IEnumerable<string> EnumerateTextDataEntries(object? textData)
    {
        switch (textData)
        {
            case null:
                yield break;
            case string text:
                foreach (var entry in SplitUriListEntries(text))
                    yield return entry;
                break;
            case IEnumerable<string> entries:
                foreach (var entry in entries)
                foreach (var value in SplitUriListEntries(entry))
                    yield return value;
                break;
            default:
                var asString = textData.ToString();
                if (!string.IsNullOrWhiteSpace(asString))
                    foreach (var entry in SplitUriListEntries(asString))
                        yield return entry;
                break;
        }
    }

    private static IEnumerable<string> SplitUriListEntries(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            yield break;

        var segments = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (trimmed.Length == 0)
                continue;

            if (trimmed[0] == '#')
                continue;

            yield return trimmed;
        }
    }

    private static bool IsUnityPackage(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
            return false;

        var extension = Path.GetExtension(pathOrName);
        return string.Equals(extension, UnityPackageExtension, StringComparison.OrdinalIgnoreCase);
    }
}
