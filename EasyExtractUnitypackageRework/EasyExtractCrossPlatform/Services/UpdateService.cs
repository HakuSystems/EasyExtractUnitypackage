using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.Services;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(UpdateSettings settings, Version? currentVersion,
        CancellationToken cancellationToken = default);

    Task<UpdateInstallResult> DownloadAndPrepareUpdateAsync(UpdateManifest manifest,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default);

    bool TryLaunchPreparedUpdate(UpdatePreparation preparation);
}

public sealed partial class UpdateService : IUpdateService
{
    private const string UserAgent =
        "EasyExtractCrossPlatform-Updater/1.0 (+https://github.com/HakuSystems/EasyExtractUnitypackage)";

    private const string WindowsOnlyUpdateMessage = "Windows-only update detected";

    private static readonly HttpClient HttpClient = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Version LegacyWindowsOnlyMaxVersion = new(2, 0, 7, 0);
    private static readonly string[] LegacyWindowsOnlyExtensions = { ".rar" };
    private static readonly string[] LegacyWindowsOnlyNameHints = { "easyextractpublish" };

    private static readonly Dictionary<string, string[]> ArchitectureAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "x64", new[] { "x64", "x86_64", "amd64" } },
        { "x86", new[] { "x86", "win32", "ia32" } },
        { "arm64", new[] { "arm64", "aarch64" } },
        { "arm", new[] { "arm", "armhf" } },
        { "s390x", new[] { "s390x" } }
    };

    public static UpdateService Instance { get; } = new();
}
