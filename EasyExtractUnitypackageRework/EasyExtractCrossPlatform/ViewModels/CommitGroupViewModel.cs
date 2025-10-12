using System.Collections.Generic;
using System.Collections.ObjectModel;
using EasyExtractCrossPlatform.Models;

namespace EasyExtractCrossPlatform.ViewModels;

public sealed class CommitGroupViewModel
{
    public CommitGroupViewModel(string key, string displayName, IEnumerable<GitCommitInfo> commits)
    {
        Key = key;
        DisplayName = displayName;
        Commits = new ObservableCollection<GitCommitInfo>(commits);
    }

    public string Key { get; }

    public string DisplayName { get; }

    public ObservableCollection<GitCommitInfo> Commits { get; }
}