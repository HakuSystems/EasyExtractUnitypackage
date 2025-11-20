namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private void AddOrUpdateQueueDisplayItem(UnityPackageFile package, string? normalizedPath = null)
    {
        if (package is null)
            return;

        var key = !string.IsNullOrWhiteSpace(normalizedPath)
            ? normalizedPath!
            : TryNormalizeFilePath(package.FilePath);

        if (string.IsNullOrWhiteSpace(key))
            return;

        if (_queueItemsByPath.TryGetValue(key, out var existing))
        {
            existing.UpdateFrom(package);
            ApplySecurityResultToDisplay(key, existing);
        }
        else
        {
            var display = new QueueItemDisplay(package, key);
            _queueItems.Add(display);
            _queueItemsByPath[key] = display;
            ApplySecurityResultToDisplay(key, display);
        }

        StartSecurityScanForPackage(key);
    }

    private sealed class QueueItemDisplay : INotifyPropertyChanged
    {
        private bool _isExtracting;
        private string? _securityInfoText;
        private string? _securityWarningText;

        public QueueItemDisplay(UnityPackageFile source, string normalizedPath)
        {
            NormalizedPath = normalizedPath;
            FilePath = string.IsNullOrWhiteSpace(source.FilePath) ? normalizedPath : source.FilePath;
            FileName = string.IsNullOrWhiteSpace(source.FileName)
                ? Path.GetFileName(FilePath)
                : source.FileName;
            FileSizeBytes = ParseFileSize(source.FileSize);
            LastUpdated = ParseLastUpdated(source.FileDate);
            UpdateFrom(source);
        }

        public string NormalizedPath { get; }

        public string FilePath { get; }

        public string FileName { get; }

        public long FileSizeBytes { get; }

        public DateTimeOffset? LastUpdated { get; }

        public string SizeText => FormatFileSize(FileSizeBytes);

        public string StatusText => IsExtracting ? "Extracting..." : "Queued";

        public string LocationText => string.IsNullOrWhiteSpace(FilePath)
            ? "Location unavailable"
            : Path.GetDirectoryName(FilePath) is { Length: > 0 } directory
                ? directory
                : FilePath;

        public bool HasSecurityWarning => !string.IsNullOrWhiteSpace(SecurityWarningText);

        public string? SecurityWarningText
        {
            get => _securityWarningText;
            private set
            {
                if (_securityWarningText == value)
                    return;

                _securityWarningText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSecurityWarning));
            }
        }

        public bool HasSecurityInfo => !string.IsNullOrWhiteSpace(SecurityInfoText);

        public string? SecurityInfoText
        {
            get => _securityInfoText;
            private set
            {
                if (_securityInfoText == value)
                    return;

                _securityInfoText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSecurityInfo));
            }
        }

        public bool IsExtracting
        {
            get => _isExtracting;
            private set
            {
                if (_isExtracting == value)
                    return;

                _isExtracting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void UpdateFrom(UnityPackageFile source)
        {
            IsExtracting = source.IsExtracting;
        }

        public void SetSecurityWarning(string? text)
        {
            SecurityWarningText = string.IsNullOrWhiteSpace(text) ? null : text;
            if (!string.IsNullOrWhiteSpace(SecurityWarningText))
                SecurityInfoText = null;
        }

        public void SetSecurityInfo(string? text)
        {
            SecurityInfoText = string.IsNullOrWhiteSpace(text) ? null : text;
            if (!string.IsNullOrWhiteSpace(SecurityInfoText))
                SecurityWarningText = null;
        }

        public void ClearSecurityIndicators()
        {
            if (_securityWarningText is null && _securityInfoText is null)
                return;

            _securityWarningText = null;
            _securityInfoText = null;
            OnPropertyChanged(nameof(SecurityWarningText));
            OnPropertyChanged(nameof(SecurityInfoText));
            OnPropertyChanged(nameof(HasSecurityWarning));
            OnPropertyChanged(nameof(HasSecurityInfo));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static long ParseFileSize(string? input)
        {
            if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0)
                return value;

            return 0;
        }

        private static DateTimeOffset? ParseLastUpdated(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            if (DateTimeOffset.TryParse(
                    input,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var result))
                return result;

            if (DateTimeOffset.TryParse(
                    input,
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var fallback))
                return fallback;

            return null;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes <= 0)
                return "0 B";

            var units = new[] { "B", "KB", "MB", "GB", "TB", "PB" };
            var size = (double)bytes;
            var unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return unitIndex == 0
                ? $"{bytes} {units[unitIndex]}"
                : $"{size:0.##} {units[unitIndex]}";
        }
    }
}
