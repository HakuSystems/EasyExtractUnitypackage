using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace EasyExtractCrossPlatform.Services;

internal static class EverythingSdkBootstrapper
{
    private const int DllPromotionMaxAttempts = 6;
    private const int DownloadMaxAttempts = 3;
    private static readonly TimeSpan DllPromotionInitialDelay = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan DownloadInitialRetryDelay = TimeSpan.FromMilliseconds(500);

    private static readonly Uri SdkDownloadUri = new("https://www.voidtools.com/Everything-SDK.zip");
    private static readonly HttpClient HttpClient = new();
    private static readonly SemaphoreSlim DllInstallGate = new(1, 1);
    private static readonly object InitializationLock = new();

    private static readonly string TargetDllFileName =
        Environment.Is64BitProcess ? "Everything64.dll" : "Everything32.dll";

    private static readonly IReadOnlyDictionary<string, string> ExpectedDllHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Everything64.dll"] = "C7AB8B47F7DD4C41AA735F4BA40B35AD5460A86FA7ABE0C94383F12BCE33BFB6",
            ["Everything32.dll"] = "C28CD066AF36CAE4403A9933847AFF01DB928787D86751F014A1FA60D8B97FDA"
        };

    private static Task? _initializationTask;
    private static string? _everythingDllPath;
    private static IntPtr _libraryHandle = IntPtr.Zero;
    private static bool _resolverRegistered;
    private static Func<Task<string>>? _ensureDllOverride;
    private static Action<string>? _registerResolverOverride;
    private static Action<string>? _loadLibraryOverride;
    private static Func<string>? _appDataPathOverride;
    private static Func<string, bool, bool>? _validateDllHashOverride;
    private static Func<IEnumerable<string>>? _localProbeDirectoriesOverride;

    public static Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            LoggingService.LogInformation("Everything SDK bootstrap skipped for non-Windows platform.");
            return Task.CompletedTask;
        }

        Task initTask;
        lock (InitializationLock)
        {
            if (_initializationTask is null || _initializationTask.IsFaulted || _initializationTask.IsCanceled)
                _initializationTask = InitializeAsync();

            initTask = _initializationTask;
        }

        if (initTask.IsCompleted)
            return initTask;

        return initTask.WaitAsync(cancellationToken);
    }

    private static async Task InitializeAsync()
    {
        try
        {
            LoggingService.LogInformation("Initializing Everything SDK bootstrapper.");
            var ensureDll = _ensureDllOverride ?? EnsureDllAsync;
            var registerResolver = _registerResolverOverride ?? RegisterDllImportResolver;
            var loadLibrary = _loadLibraryOverride ?? LoadLibrary;

            var dllPath = await ensureDll().ConfigureAwait(false);
            registerResolver(dllPath);
            loadLibrary(dllPath);
            LoggingService.LogInformation($"Everything SDK initialized using '{dllPath}'.");
        }
        catch (Exception ex) when (ex is HttpRequestException or HttpIOException or InvalidDataException
                                       or TaskCanceledException or TimeoutException)
        {
            var status = DescribeNetworkFailure(ex);

            LoggingService.LogError($"Failed to initialize Everything SDK due to network error ({status}).", ex,
                false);
            throw EverythingSearchException.ServiceUnavailable(status, ex);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        {
            LoggingService.LogError("Failed to initialize Everything SDK due to I/O or network error.", ex);
            throw EverythingSearchException.MissingLibrary(ex);
        }
        catch (Exception ex) when (ex is not EverythingSearchException)
        {
            LoggingService.LogError("Unexpected failure while initializing Everything SDK.", ex);
            throw EverythingSearchException.NativeFailure(ex);
        }
    }

    private static async Task<string> EnsureDllAsync()
    {
        if (_everythingDllPath is { Length: > 0 } && File.Exists(_everythingDllPath))
        {
            LoggingService.LogInformation($"Everything SDK DLL already present at '{_everythingDllPath}'.");
            return _everythingDllPath;
        }

        var appData = (_appDataPathOverride?.Invoke() ??
                       Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).Trim();
        var sdkDirectory = Path.Combine(appData, "EasyExtract", "ThirdParty", "EverythingSdk");
        Directory.CreateDirectory(sdkDirectory);

        var dllPath = Path.Combine(sdkDirectory, TargetDllFileName);

        await DllInstallGate.WaitAsync().ConfigureAwait(false);
        try
        {
            CleanupOrphanedTemporaryDlls(sdkDirectory);

            if (_everythingDllPath is { Length: > 0 } existingPath && File.Exists(existingPath))
            {
                LoggingService.LogInformation($"Everything SDK DLL already present at '{existingPath}'.");
                return existingPath;
            }

            if (await TryReuseExistingDllAsync(dllPath).ConfigureAwait(false))
            {
                _everythingDllPath = dllPath;
                return dllPath;
            }

            // The error messages tell users to drop the SDK DLL next to the executable, so honour
            // that before going to the network. It is also the only way out on an offline machine.
            if (TryUseLocallyShippedDll(out var localDllPath))
            {
                _everythingDllPath = localDllPath;
                return localDllPath;
            }

            await DownloadDllWithRetryAsync(sdkDirectory, dllPath).ConfigureAwait(false);

            _everythingDllPath = dllPath;
            return dllPath;
        }
        finally
        {
            DllInstallGate.Release();
        }
    }

    private static bool TryUseLocallyShippedDll(out string dllPath)
    {
        foreach (var directory in GetLocalProbeDirectories())
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            var candidate = Path.Combine(directory, TargetDllFileName);

            try
            {
                if (!File.Exists(candidate))
                    continue;

                // Never delete a file the user placed there, even when the hash does not match.
                if (!ValidateDllHash(candidate, false))
                {
                    LoggingService.LogWarning(
                        $"Ignoring Everything SDK DLL at '{candidate}' because it failed integrity validation.");
                    continue;
                }

                LoggingService.LogInformation($"Using locally shipped Everything SDK DLL at '{candidate}'.");
                dllPath = candidate;
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"Failed to inspect Everything SDK DLL at '{candidate}'.", ex);
            }
        }

        dllPath = string.Empty;
        return false;
    }

    private static IEnumerable<string> GetLocalProbeDirectories()
    {
        if (_localProbeDirectoriesOverride is not null)
            return _localProbeDirectoriesOverride();

        var directories = new List<string> { AppContext.BaseDirectory };

        if (Environment.ProcessPath is { Length: > 0 } processPath)
        {
            var processDirectory = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrWhiteSpace(processDirectory))
                directories.Add(processDirectory);
        }

        return directories.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task DownloadDllWithRetryAsync(string sdkDirectory, string dllPath)
    {
        var delay = DownloadInitialRetryDelay;

        for (var attempt = 1;; attempt++)
            try
            {
                await DownloadDllAsync(sdkDirectory, dllPath).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (IsTransientDownloadFailure(ex) && attempt < DownloadMaxAttempts)
            {
                LoggingService.LogWarning(
                    $"Everything SDK download attempt {attempt}/{DownloadMaxAttempts} failed ({ex.GetType().Name}). Retrying.",
                    ex);

                await Task.Delay(delay).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(Math.Min(4000, delay.TotalMilliseconds * 2));
            }
    }

    private static async Task DownloadDllAsync(string sdkDirectory, string dllPath)
    {
        var tempZipPath = Path.Combine(Path.GetTempPath(), $"Everything-SDK-{Guid.NewGuid():N}.zip");
        var stagedDllPath = Path.Combine(sdkDirectory, $"{TargetDllFileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            LoggingService.LogInformation($"Downloading Everything SDK from '{SdkDownloadUri}'.");
            using (var response = await HttpClient
                       .GetAsync(SdkDownloadUri, HttpCompletionOption.ResponseHeadersRead)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                await using var downloadStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await using (var tempFileStream = File.Create(tempZipPath))
                {
                    await downloadStream.CopyToAsync(tempFileStream).ConfigureAwait(false);
                }
            }

            LoggingService.LogInformation($"Everything SDK archive downloaded to '{tempZipPath}'.");

            using var archive = ZipFile.OpenRead(tempZipPath);
            var dllEntry = archive.Entries
                .FirstOrDefault(entry =>
                    entry.FullName.EndsWith(TargetDllFileName, StringComparison.OrdinalIgnoreCase));

            if (dllEntry is null)
                throw new InvalidOperationException(
                    $"The Everything SDK package did not contain {TargetDllFileName}.");

            await using (var entryStream = dllEntry.Open())
            await using (var stagedStream = File.Create(stagedDllPath))
            {
                await entryStream.CopyToAsync(stagedStream).ConfigureAwait(false);
            }

            await PromoteStagedDllAsync(
                    stagedDllPath,
                    dllPath,
                    DllPromotionMaxAttempts,
                    DllPromotionInitialDelay)
                .ConfigureAwait(false);

            if (!ValidateDllHash(dllPath, false))
            {
                LoggingService.LogError("The promoted Everything SDK DLL failed integrity validation.");
                throw new InvalidOperationException("The promoted Everything SDK DLL failed integrity validation.");
            }

            LoggingService.LogInformation($"Everything SDK DLL extracted to '{dllPath}'.");
        }
        finally
        {
            TryDeleteFile(tempZipPath);
            TryDeleteFile(stagedDllPath);
        }
    }

    /// <summary>
    ///     A truncated response, a timeout or a corrupt archive all mean the same thing: the download
    ///     did not survive the trip. Retrying is worthwhile, telling the user their DLL is missing is not.
    /// </summary>
    internal static bool IsTransientDownloadFailure(Exception? exception)
    {
        return exception switch
        {
            HttpIOException => true,
            HttpRequestException => true,
            InvalidDataException => true,
            TaskCanceledException => true,
            TimeoutException => true,
            _ => false
        };
    }

    private static string DescribeNetworkFailure(Exception exception)
    {
        return exception switch
        {
            HttpRequestException { StatusCode: HttpStatusCode code } => $"{(int)code} {code}",
            HttpIOException => "The download from voidtools.com ended prematurely",
            InvalidDataException => "The downloaded Everything SDK archive was corrupt",
            TaskCanceledException or TimeoutException => "The download from voidtools.com timed out",
            _ => "No response from voidtools.com"
        };
    }

    private static async Task<bool> TryReuseExistingDllAsync(string dllPath)
    {
        if (!File.Exists(dllPath))
            return false;

        var validationDelay = TimeSpan.FromMilliseconds(20);
        for (var attempt = 1; attempt <= 4; attempt++)
            try
            {
                if (!ValidateDllHash(dllPath))
                {
                    LoggingService.LogInformation(
                        $"Discarding Everything SDK DLL at '{dllPath}' due to hash mismatch.");
                    return false;
                }

                LoggingService.LogInformation($"Reusing previously downloaded Everything SDK DLL at '{dllPath}'.");
                return true;
            }
            catch (Exception ex) when (IsTransientFileLock(ex) && attempt < 4)
            {
                await Task.Delay(validationDelay).ConfigureAwait(false);
                validationDelay = TimeSpan.FromMilliseconds(Math.Min(200, validationDelay.TotalMilliseconds * 2));
            }
            catch (Exception ex) when (IsTransientFileLock(ex))
            {
                LoggingService.LogWarning(
                    $"Everything SDK DLL at '{dllPath}' is locked by another process. Reusing existing file.",
                    ex);
                return true;
            }

        return false;
    }

    private static void CleanupOrphanedTemporaryDlls(string sdkDirectory)
    {
        try
        {
            if (!Directory.Exists(sdkDirectory))
                return;

            foreach (var path in Directory.EnumerateFiles(sdkDirectory, "*.tmp", SearchOption.TopDirectoryOnly))
            {
                if (!IsTemporarySdkDllName(Path.GetFileName(path)))
                    continue;

                TryDeleteFile(path);
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogWarning($"Failed to clean Everything SDK temporary files in '{sdkDirectory}'.", ex);
        }
    }

    private static async Task PromoteStagedDllAsync(
        string stagedDllPath,
        string destinationPath,
        int maxAttempts,
        TimeSpan initialDelay)
    {
        if (!File.Exists(stagedDllPath))
            return;

        var delay = initialDelay;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
            try
            {
                File.Move(stagedDllPath, destinationPath, true);
                return;
            }
            catch (Exception ex) when (IsTransientFileLock(ex) && attempt < maxAttempts)
            {
                if (File.Exists(destinationPath))
                    try
                    {
                        if (ValidateDllHash(destinationPath, false))
                            return;
                    }
                    catch (Exception validateEx) when (IsTransientFileLock(validateEx))
                    {
                    }

                LoggingService.LogWarning(
                    $"Everything SDK DLL promotion attempt {attempt}/{maxAttempts} encountered a sharing violation. Retrying.");
                await Task.Delay(delay).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(Math.Min(500, delay.TotalMilliseconds * 2));
            }
            catch (Exception ex) when (IsTransientFileLock(ex))
            {
                throw new IOException(
                    $"Unable to finalize Everything SDK DLL at '{destinationPath}' after repeated sharing violations.",
                    ex);
            }

        if (File.Exists(destinationPath) && ValidateDllHash(destinationPath, false))
            return;

        throw new IOException($"Unable to finalize Everything SDK DLL at '{destinationPath}'.");
    }

    private static bool IsSharingViolation(IOException ex)
    {
        return ex.HResult == unchecked((int)0x80070020);
    }

    private static bool IsTransientFileLock(Exception ex)
    {
        if (ex is IOException ioEx)
            return IsSharingViolation(ioEx) || ioEx.HResult == unchecked((int)0x80070005);

        if (ex is UnauthorizedAccessException unauthorizedEx)
            return unauthorizedEx.HResult == unchecked((int)0x80070005);

        return false;
    }

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            LoggingService.LogWarning($"Failed to delete temporary file '{path}'.", ex);
        }
    }

    private static void RegisterDllImportResolver(string dllPath)
    {
        if (_resolverRegistered)
            return;

        NativeLibrary.SetDllImportResolver(typeof(EverythingNative).Assembly, ResolveImport);
        _resolverRegistered = true;
        LoggingService.LogInformation($"Registered DllImport resolver for Everything SDK using '{dllPath}'.");

        IntPtr ResolveImport(string libraryName, Assembly _, DllImportSearchPath? __)
        {
            if (!libraryName.Equals("Everything64.dll", StringComparison.OrdinalIgnoreCase) &&
                !libraryName.Equals("Everything32.dll", StringComparison.OrdinalIgnoreCase))
                return IntPtr.Zero;

            if (_libraryHandle != IntPtr.Zero)
                return _libraryHandle;

            if (!File.Exists(dllPath))
                throw new FileNotFoundException("Everything SDK DLL is missing.", dllPath);

            _libraryHandle = NativeLibrary.Load(dllPath);
            LoggingService.LogInformation($"Loaded Everything SDK DLL from '{dllPath}'.");
            return _libraryHandle;
        }
    }

    private static void LoadLibrary(string dllPath)
    {
        if (_libraryHandle != IntPtr.Zero)
            return;

        if (!File.Exists(dllPath))
            throw new FileNotFoundException("Everything SDK DLL is missing.", dllPath);

        _libraryHandle = NativeLibrary.Load(dllPath);
        LoggingService.LogInformation($"Loaded Everything SDK DLL via NativeLibrary.Load from '{dllPath}'.");
    }

    private static bool ValidateDllHash(string dllPath, bool deleteOnMismatch = true)
    {
        if (_validateDllHashOverride is not null)
            return _validateDllHashOverride(dllPath, deleteOnMismatch);

        var fileName = Path.GetFileName(dllPath);
        if (IsTemporarySdkDllName(fileName))
        {
            if (deleteOnMismatch)
                TryDeleteFile(dllPath);

            LoggingService.LogInformation($"Ignoring temporary Everything SDK file '{dllPath}'.");
            return false;
        }

        if (!ExpectedDllHashes.TryGetValue(fileName, out var expectedHash))
        {
            if (deleteOnMismatch)
                TryDeleteFile(dllPath);

            LoggingService.LogWarning($"No expected hash registered for Everything SDK file '{fileName}'.");
            return false;
        }

        string actualHash;
        // Tolerant sharing flags: another instance may hold the DLL open (loaded library
        // or an in-flight download) and File.OpenRead would fail with a sharing violation.
        using (var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(stream);
            actualHash = Convert.ToHexString(hashBytes);
        }

        if (actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            return true;

        if (deleteOnMismatch)
            TryDeleteFile(dllPath);

        LoggingService.LogError(
            $"Everything SDK DLL at '{dllPath}' failed validation. Expected {expectedHash}, actual {actualHash}.");
        return false;
    }

    private static bool IsTemporarySdkDllName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            return false;

        return fileName.StartsWith("Everything64.dll.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("Everything32.dll.", StringComparison.OrdinalIgnoreCase);
    }

    internal static void ConfigureForTests(
        Func<Task<string>>? ensureDllOverride = null,
        Action<string>? registerResolverOverride = null,
        Action<string>? loadLibraryOverride = null,
        Func<string>? appDataPathOverride = null,
        Func<string, bool, bool>? validateDllHashOverride = null,
        Func<IEnumerable<string>>? localProbeDirectoriesOverride = null)
    {
        _ensureDllOverride = ensureDllOverride;
        _registerResolverOverride = registerResolverOverride;
        _loadLibraryOverride = loadLibraryOverride;
        _appDataPathOverride = appDataPathOverride;
        _validateDllHashOverride = validateDllHashOverride;
        _localProbeDirectoriesOverride = localProbeDirectoriesOverride;
    }

    internal static void ResetForTests()
    {
        lock (InitializationLock)
        {
            _initializationTask = null;
        }

        _everythingDllPath = null;
        _ensureDllOverride = null;
        _registerResolverOverride = null;
        _loadLibraryOverride = null;
        _appDataPathOverride = null;
        _validateDllHashOverride = null;
        _localProbeDirectoriesOverride = null;
    }

    internal static Task<string> EnsureDllForTestsAsync()
    {
        return EnsureDllAsync();
    }

    internal static bool TryUseLocallyShippedDllForTests(out string dllPath)
    {
        return TryUseLocallyShippedDll(out dllPath);
    }

    internal static Task PromoteStagedDllForTestsAsync(
        string stagedDllPath,
        string destinationPath,
        int maxAttempts,
        TimeSpan initialDelay)
    {
        return PromoteStagedDllAsync(stagedDllPath, destinationPath, maxAttempts, initialDelay);
    }

    internal static bool ValidateDllHashForTests(string dllPath, bool deleteOnMismatch = true)
    {
        return ValidateDllHash(dllPath, deleteOnMismatch);
    }
}