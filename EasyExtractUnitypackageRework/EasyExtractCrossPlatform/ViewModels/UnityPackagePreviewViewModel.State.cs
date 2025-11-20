using System;

namespace EasyExtractCrossPlatform.ViewModels;

public sealed partial class UnityPackagePreviewViewModel
{
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (value == _isLoading)
                return;

            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (value == _hasError)
                return;

            _hasError = value;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (string.Equals(_statusMessage, value, StringComparison.Ordinal))
                return;

            _statusMessage = value;
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    public string PackageName
    {
        get => _packageName;
        private set
        {
            if (string.Equals(_packageName, value, StringComparison.Ordinal))
                return;

            _packageName = value ?? string.Empty;
            OnPropertyChanged(nameof(PackageName));
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public string PackagePath { get; }

    public string PackageSizeText
    {
        get => _packageSizeText;
        private set
        {
            if (string.Equals(_packageSizeText, value, StringComparison.Ordinal))
                return;

            _packageSizeText = value ?? string.Empty;
            OnPropertyChanged(nameof(PackageSizeText));
        }
    }

    public string TotalAssetSizeText
    {
        get => _totalAssetSizeText;
        private set
        {
            if (string.Equals(_totalAssetSizeText, value, StringComparison.Ordinal))
                return;

            _totalAssetSizeText = value ?? string.Empty;
            OnPropertyChanged(nameof(TotalAssetSizeText));
        }
    }

    public string? PackageModifiedText
    {
        get => _packageModifiedText;
        private set
        {
            if (string.Equals(_packageModifiedText, value, StringComparison.Ordinal))
                return;

            _packageModifiedText = value;
            OnPropertyChanged(nameof(PackageModifiedText));
        }
    }

    public string WindowTitle => string.IsNullOrWhiteSpace(PackageName)
        ? "Package Preview"
        : $"Preview - {PackageName}";
}
