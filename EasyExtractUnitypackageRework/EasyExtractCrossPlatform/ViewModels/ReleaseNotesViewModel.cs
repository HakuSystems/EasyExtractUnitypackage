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

public class ReleaseNotesViewModel : INotifyPropertyChanged, IDisposable
{
    private string? _errorMessage;
    private bool _isLoading;
    private DateTimeOffset? _lastUpdated;
    private CancellationTokenSource? _loadingCts;

    public ReleaseNotesViewModel()
    {
        Commits.CollectionChanged += HandleCommitsChanged;
    }

    public ObservableCollection<GitCommitInfo> Commits { get; } = new();

    public ObservableCollection<CommitGroupViewModel> CommitGroups { get; } = new();

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
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasCommits => Commits.Count > 0;

    public bool HasNoCommits => !HasCommits;

    public bool ShowEmptyState => !HasCommits && !IsLoading && !HasError;

    public void Dispose()
    {
        CancelLoading();
        Commits.CollectionChanged -= HandleCommitsChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadAsync(int maxCommits = 30)
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

            var commits = await GitHubReleaseNotesService.GetRecentCommitsAsync(maxCommits, cts.Token)
                .ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateCommitCollections(commits);
                LastUpdated = DateTimeOffset.Now;
            });
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // ignore cancellations triggered by the view model
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void HandleCommitsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasCommits));
        OnPropertyChanged(nameof(HasNoCommits));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void UpdateCommitCollections(IReadOnlyList<GitCommitInfo> commits)
    {
        Commits.Clear();
        CommitGroups.Clear();

        if (commits.Count == 0)
            return;

        var categoryOrder = new List<string>();
        var groupedCommits = new Dictionary<string, List<GitCommitInfo>>();

        foreach (var commit in commits)
        {
            Commits.Add(commit);

            var key = commit.CategoryKey;
            if (!groupedCommits.TryGetValue(key, out var list))
            {
                list = new List<GitCommitInfo>();
                groupedCommits[key] = list;
                categoryOrder.Add(key);
            }

            list.Add(commit);
        }

        foreach (var key in categoryOrder)
        {
            if (!groupedCommits.TryGetValue(key, out var list) || list.Count == 0)
                continue;

            var displayName = list[0].CategoryDisplayName;
            CommitGroups.Add(new CommitGroupViewModel(key, displayName, list));
        }
    }
}