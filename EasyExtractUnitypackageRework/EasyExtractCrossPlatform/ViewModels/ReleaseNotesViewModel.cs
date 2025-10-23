using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;

namespace EasyExtractCrossPlatform.ViewModels;

public sealed class ReleaseNotesViewModel : INotifyPropertyChanged, IDisposable
{
    private string? _errorMessage;
    private GitReleaseInfo? _featuredRelease;
    private bool _isLoading;
    private DateTimeOffset? _lastUpdated;
    private CancellationTokenSource? _loadingCts;

    public ReleaseNotesViewModel()
    {
        AdditionalReleases.CollectionChanged += HandleAdditionalReleasesChanged;
    }

    public ObservableCollection<GitReleaseInfo> AdditionalReleases { get; } = new();

    public GitReleaseInfo? FeaturedRelease
    {
        get => _featuredRelease;
        private set
        {
            if (ReferenceEquals(value, _featuredRelease))
                return;

            _featuredRelease = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasFeaturedRelease));
            OnPropertyChanged(nameof(HasReleases));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (value == _isLoading)
                return;

            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (value == _errorMessage)
                return;

            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public DateTimeOffset? LastUpdated
    {
        get => _lastUpdated;
        private set
        {
            if (value == _lastUpdated)
                return;

            _lastUpdated = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasLastUpdated));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasFeaturedRelease => FeaturedRelease is not null;

    public bool HasAdditionalReleases => AdditionalReleases.Count > 0;

    public bool HasReleases => HasFeaturedRelease || HasAdditionalReleases;

    public bool ShowEmptyState => !HasReleases && !IsLoading && !HasError;

    public bool HasLastUpdated => LastUpdated.HasValue;

    public void Dispose()
    {
        CancelLoading();
        AdditionalReleases.CollectionChanged -= HandleAdditionalReleasesChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadAsync(int maxReleases = 0)
    {
        if (IsLoading)
            return;

        CancelLoading();

        var cts = new CancellationTokenSource();
        _loadingCts = cts;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            IReadOnlyList<GitReleaseInfo> releases;
            if (maxReleases > 0)
                releases = await GitHubReleaseNotesService.GetRecentReleasesAsync(maxReleases, cts.Token)
                    .ConfigureAwait(false);
            else
                releases = await GitHubReleaseNotesService.GetAllReleasesAsync(cts.Token)
                    .ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateReleaseCollections(releases);
                LastUpdated = DateTimeOffset.Now;
            });
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // ignore user cancellations
        }
        catch (HttpRequestException httpEx)
        {
            ErrorMessage = $"Unable to reach GitHub: {httpEx.Message}";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Loading release notes timed out. Please try again.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error while loading release notes: {ex.Message}";
        }
        finally
        {
            if (!cts.IsCancellationRequested)
                IsLoading = false;

            if (_loadingCts == cts)
                _loadingCts = null;
        }
    }

    public void CancelLoading()
    {
        if (_loadingCts is null)
            return;

        if (!_loadingCts.IsCancellationRequested)
            _loadingCts.Cancel();

        _loadingCts.Dispose();
        _loadingCts = null;
    }

    private void UpdateReleaseCollections(IReadOnlyList<GitReleaseInfo> releases)
    {
        FeaturedRelease = releases.Count > 0 ? releases[0] : null;

        AdditionalReleases.CollectionChanged -= HandleAdditionalReleasesChanged;
        AdditionalReleases.Clear();
        for (var i = 1; i < releases.Count; i++)
            AdditionalReleases.Add(releases[i]);
        AdditionalReleases.CollectionChanged += HandleAdditionalReleasesChanged;

        OnPropertyChanged(nameof(HasAdditionalReleases));
        OnPropertyChanged(nameof(HasReleases));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void HandleAdditionalReleasesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasAdditionalReleases));
        OnPropertyChanged(nameof(HasReleases));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}