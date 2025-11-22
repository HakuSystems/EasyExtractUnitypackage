namespace EasyExtractCrossPlatform.Models;

public enum UpdatePhase
{
    Checking,
    Downloading,
    Extracting,
    Preparing,
    WaitingForRestart,
    Completed
}

public sealed record ReleaseAssetInfo(
    string Name,
    Uri DownloadUri,
    long Size,
    string? ContentType);

public sealed record UpdateManifest(
    string RepositoryOwner,
    string RepositoryName,
    string TagName,
    Version Version,
    string? ReleaseName,
    string? ReleaseNotes,
    DateTimeOffset? PublishedAt,
    ReleaseAssetInfo Asset,
    Uri? ReleasePage);

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    UpdateManifest? Manifest,
    string? Message = null);

public readonly record struct UpdateProgress(
    UpdatePhase Phase,
    double? Percentage,
    long BytesTransferred = 0,
    long? TotalBytes = null);

public sealed class UpdatePreparation
{
    public required Version TargetVersion { get; init; }
    public required string StageDirectory { get; init; }
    public required string SourceDirectory { get; init; }
    public required string ScriptPath { get; init; }
    public required string[] ScriptArguments { get; init; }
    public required string WorkingDirectory { get; init; }
    public string? ReleaseName { get; init; }
    public string? ReleaseNotes { get; init; }
}

public sealed class UpdateInstallResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
    public UpdatePreparation? Preparation { get; init; }

    public static UpdateInstallResult FromPreparation(UpdatePreparation preparation)
    {
        return new UpdateInstallResult { Success = true, Preparation = preparation };
    }

    public static UpdateInstallResult Failed(string error, Exception? exception = null)
    {
        return new UpdateInstallResult { Success = false, ErrorMessage = error, Exception = exception };
    }
}

public static class UpdateManifestExtensions
{
    public static string GetDisplayName(this UpdateManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.ReleaseName))
            return manifest.ReleaseName!;

        if (!string.IsNullOrWhiteSpace(manifest.TagName))
            return manifest.TagName;

        return $"v{manifest.Version}";
    }
}