using System.Resources;

namespace EasyExtractCrossPlatform.Localization;

/// <summary>
///     Centralizes access to localized strings backed by embedded .resx resources.
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> LazyInstance = new(() => new LocalizationManager());

    private readonly ResourceManager _resourceManager =
        new("EasyExtractCrossPlatform.Resources.Strings", typeof(LocalizationManager).Assembly);

    private CultureInfo _currentCulture;

    private LocalizationManager()
    {
        _currentCulture = CultureInfo.CurrentUICulture;
    }

    public static LocalizationManager Instance => LazyInstance.Value;

    /// <summary>
    ///     Gets or sets the culture used for resource lookups. Setting the property notifies listeners.
    /// </summary>
    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture.Equals(value)) return;

            _currentCulture = value;
            CultureInfo.CurrentCulture = value;
            CultureInfo.CurrentUICulture = value;
            OnPropertyChanged(nameof(CurrentCulture));
            OnPropertyChanged("Item[]");
        }
    }

    /// <summary>
    ///     Indexer to facilitate binding directly to localized values.
    /// </summary>
    public string this[string key] => GetString(key);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Returns a localized string for the provided key, falling back to the key when missing.
    /// </summary>
    public string GetString(string key)
    {
        return _resourceManager.GetString(key, _currentCulture) ?? key;
    }

    /// <summary>
    ///     Returns a formatted localized string using the current culture.
    /// </summary>
    public string GetString(string key, params object?[] args)
    {
        var format = GetString(key);
        return args.Length == 0 ? format : string.Format(_currentCulture, format, args);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}