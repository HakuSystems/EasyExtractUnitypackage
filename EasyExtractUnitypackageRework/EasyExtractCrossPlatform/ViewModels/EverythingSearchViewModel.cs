using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.ViewModels;

public sealed class EverythingSearchViewModel : INotifyPropertyChanged, IDisposable
{
    private const int DefaultMaxResults = 200;
    private const string DefaultStatus = "";
    private static readonly TimeSpan AutoSearchInterval = TimeSpan.FromMilliseconds(250);
    private readonly DispatcherTimer _autoSearchTimer;

    private readonly IEverythingSearchService _searchService;
    private CancellationTokenSource? _activeSearchCts;
    private bool _hasError;
    private bool _isEverythingAvailable;
    private bool _isSearching;
    private string _searchQuery = string.Empty;
    private string? _statusMessage = DefaultStatus;

    public EverythingSearchViewModel()
        : this(AppServiceLocator.Current.GetRequiredService<IEverythingSearchService>())
    {
    }

    public EverythingSearchViewModel(IEverythingSearchService searchService)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));

        Results.CollectionChanged += HandleResultsChanged;

        _autoSearchTimer = new DispatcherTimer
        {
            Interval = AutoSearchInterval,
            IsEnabled = false
        };
        _autoSearchTimer.Tick += OnAutoSearchTimerTick;

        SearchCommand = new AsyncRelayCommand(_ => ExecuteSearchAsync(), _ => !IsSearching && IsEverythingAvailable);
        OpenItemCommand = new RelayCommand(HandleAddToQueue);
        RevealItemCommand = new RelayCommand(HandleOpenDirectory);
        ClearCommand = new RelayCommand(ClearResults, _ => HasResults || !string.IsNullOrWhiteSpace(SearchQuery));
    }

    public ObservableCollection<EverythingSearchResult> Results { get; } = new();

    public AsyncRelayCommand SearchCommand { get; }

    public RelayCommand OpenItemCommand { get; }

    public RelayCommand RevealItemCommand { get; }

    public RelayCommand ClearCommand { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (string.Equals(_searchQuery, value, StringComparison.Ordinal))
                return;

            _searchQuery = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInteractionActive));
            SearchCommand.RaiseCanExecuteChanged();
            ClearCommand.RaiseCanExecuteChanged();
            ScheduleAutoSearch();
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set
        {
            if (value == _isSearching)
                return;

            _isSearching = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInteractionActive));
            SearchCommand.RaiseCanExecuteChanged();
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
            OnPropertyChanged();
        }
    }

    public bool IsEverythingAvailable
    {
        get => _isEverythingAvailable;
        private set
        {
            if (value == _isEverythingAvailable)
                return;

            _isEverythingAvailable = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowUnavailableNotice));
            SearchCommand.RaiseCanExecuteChanged();
            if (!value)
                _autoSearchTimer.Stop();
        }
    }

    public bool ShowUnavailableNotice => !IsEverythingAvailable;

    public bool HasResults => Results.Count > 0;

    public bool IsInteractionActive => IsSearching || HasResults || !string.IsNullOrWhiteSpace(SearchQuery);

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (string.Equals(_statusMessage, value, StringComparison.Ordinal))
                return;

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool ShowStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public void Dispose()
    {
        CancelActiveSearch();
        Results.CollectionChanged -= HandleResultsChanged;
        _activeSearchCts?.Dispose();
        _activeSearchCts = null;
        _autoSearchTimer.Tick -= OnAutoSearchTimerTick;
        _autoSearchTimer.Stop();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            LoggingService.LogInformation("Initializing EverythingSearchViewModel.");
            var available = await _searchService.IsAvailableAsync(cancellationToken).ConfigureAwait(true);
            IsEverythingAvailable = available;
            HasError = !available;

            var hint = _searchService.AvailabilityHint;
            if (available)
            {
                HasError = false;
                StatusMessage = string.IsNullOrWhiteSpace(hint) ? DefaultStatus : hint;
                LoggingService.LogInformation("Search backend is available.");
            }
            else
            {
                StatusMessage = string.IsNullOrWhiteSpace(hint)
                    ? "Search backend is not available on this platform."
                    : hint;
                LoggingService.LogInformation("Search backend is unavailable.");
            }
        }
        catch (EverythingSearchException ex)
        {
            IsEverythingAvailable = false;
            HasError = true;
            StatusMessage = ex.Message;
            LoggingService.LogError("Search initialization failed with EverythingSearchException.", ex);
        }
        catch (InvalidOperationException ex)
        {
            IsEverythingAvailable = false;
            HasError = true;
            StatusMessage = ex.Message;
            LoggingService.LogError("Search initialization failed with InvalidOperationException.", ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ignore initialization cancellation.
            LoggingService.LogInformation("Search initialization cancelled.");
        }
    }

    public event EventHandler<string>? AddToQueueRequested;

    private async Task ExecuteSearchAsync()
    {
        _autoSearchTimer.Stop();

        var query = SearchQuery.Trim();
        LoggingService.LogInformation($"Executing Everything search from view model. Query='{query}'.");

        if (query.Length == 0)
        {
            LoggingService.LogInformation("Search query was empty; clearing results.");
            CancelActiveSearch();
            Results.Clear();
            HasError = false;
            StatusMessage = DefaultStatus;
            return;
        }

        CancelActiveSearch();
        var cts = new CancellationTokenSource();
        _activeSearchCts = cts;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            IsSearching = true;
            HasError = false;
            StatusMessage = "Searching...";

            var results = await _searchService.SearchAsync(query, DefaultMaxResults, cts.Token)
                .ConfigureAwait(true);

            if (cts.IsCancellationRequested)
                return;

            Results.Clear();
            foreach (var result in results)
                Results.Add(result);

            var excludedCount = _searchService.LastExcludedResultCount;
            if (Results.Count > 0)
                StatusMessage = excludedCount > 0
                    ? $"Showing {Results.Count} result(s). Skipped {excludedCount} from recycle bins or unreadable files."
                    : $"Showing {Results.Count} result(s).";
            else
                StatusMessage = excludedCount > 0
                    ? "Only recycle-bin or unreadable entries were found."
                    : "No files or folders matched your search.";

            stopwatch.Stop();
            LoggingService.LogInformation(
                $"Search completed in {stopwatch.Elapsed.TotalMilliseconds:F0} ms. Results={Results.Count}, excluded={excludedCount}.");
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Suppress cancellation noise.
        }
        catch (EverythingSearchException ex)
        {
            if (!cts.IsCancellationRequested)
            {
                Results.Clear();
                HasError = true;
                StatusMessage = ex.Message;
                stopwatch.Stop();
                LoggingService.LogError("Search failed with EverythingSearchException.", ex);
            }
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                Results.Clear();
                HasError = true;
                StatusMessage = $"Search failed: {ex.Message}";
                stopwatch.Stop();
                LoggingService.LogError("Unexpected exception during search.", ex);
            }
        }
        finally
        {
            stopwatch.Stop();
            if (_activeSearchCts == cts)
            {
                _activeSearchCts.Dispose();
                _activeSearchCts = null;
                IsSearching = false;
            }
        }
    }

    private void HandleAddToQueue(object? parameter)
    {
        if (parameter is not EverythingSearchResult result)
            return;

        LoggingService.LogInformation($"Queueing package '{result.FullPath}'.");
        try
        {
            if (string.IsNullOrWhiteSpace(result.FullPath) || !File.Exists(result.FullPath))
                throw new FileNotFoundException("Package no longer exists.", result.FullPath);

            if (AddToQueueRequested is null)
                throw new InvalidOperationException("Queue handler is not available.");

            AddToQueueRequested.Invoke(this, result.FullPath);

            HasError = false;
            StatusMessage = $"Queued {result.Name}.";
            LoggingService.LogInformation($"Package '{result.FullPath}' queued successfully.");
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Unable to queue {result.Name}: {ex.Message}";
            LoggingService.LogError($"Failed to queue package '{result.FullPath}'.", ex);
        }
    }

    private void HandleOpenDirectory(object? parameter)
    {
        if (parameter is not EverythingSearchResult result)
            return;

        LoggingService.LogInformation($"Opening directory for '{result.FullPath}'.");
        try
        {
            var directory = result.IsFolder
                ? result.FullPath
                : result.DirectoryPath;

            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                throw new DirectoryNotFoundException("Directory no longer exists.");

            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });

            HasError = false;
            StatusMessage = $"Opened directory for {result.Name}.";
            LoggingService.LogInformation($"Directory opened for '{result.FullPath}'.");
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Unable to open directory for {result.Name}: {ex.Message}";
            LoggingService.LogError($"Failed to open directory for '{result.FullPath}'.", ex);
        }
    }

    private void ClearResults(object? _)
    {
        LoggingService.LogInformation("Clearing search results.");
        CancelActiveSearch();
        _autoSearchTimer.Stop();
        if (!string.IsNullOrEmpty(SearchQuery))
        {
            SearchQuery = string.Empty;
            _autoSearchTimer.Stop();
        }

        Results.Clear();
        HasError = false;
        StatusMessage = DefaultStatus;
        OnPropertyChanged(nameof(IsInteractionActive));
    }

    private void CancelActiveSearch()
    {
        if (_activeSearchCts is null)
            return;

        if (!_activeSearchCts.IsCancellationRequested)
            _activeSearchCts.Cancel();
    }

    private void HandleResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(IsInteractionActive));
        ClearCommand.RaiseCanExecuteChanged();
    }

    private void ScheduleAutoSearch()
    {
        if (!IsEverythingAvailable)
            return;

        _autoSearchTimer.Stop();
        _autoSearchTimer.Start();
    }

    private void OnAutoSearchTimerTick(object? sender, EventArgs e)
    {
        _autoSearchTimer.Stop();
        _ = ExecuteSearchAsync();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
