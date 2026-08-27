using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace EasyExtractCrossPlatform.Services;

internal static class EverythingSdkBootstrapper
{
    private static readonly SemaphoreSlim DllInstallGate = new(1, 1);
    private static readonly object InitializationLock = new();

    private static readonly string TargetDllFileName =
        Environment.Is64BitProcess ? "Everything64.dll" : "Everything32.dll";

    private static readonly IReadOnlyDictionary<string, string> ExpectedDllHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Both measured against the Voidtools SDK archive on 2026-08-27. Only the x64 file is
            // bundled; the x86 entry exists to validate a DLL a user drops next to the executable.
            ["Everything64.dll"] = "81B5BE18126ACD2C2B913F8F4A821E476B18393CDD3DEBD03387C50AFD8DB88F",
            ["Everything32.dll"] = "1FFE69D856A8A071A7531D54D7B05AE04A87814B812E649521A67A606C7F93E0"
        };

    private static Task? _initializationTask;
    private static string? _everythingDllPath;
    private static IntPtr _libraryHandle = IntPtr.Zero;
    private static bool _resolverRegistered;
    private static Func<Task<string>>? _ensureDllOverride;
    private static Action<string>? _registerResolverOverride;
    private static Action<string>? _loadLibraryOverride;
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
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        {
            LoggingService.LogError("Failed to locate or load the Everything SDK DLL.", ex);
            throw EverythingSearchException.MissingLibrary(ex);
        }
        catch (Exception ex) when (ex is not EverythingSearchException)
        {
            LoggingService.LogError("Unexpected failure while initializing Everything SDK.", ex);
            throw EverythingSearchException.NativeFailure(ex);
        }
    }

    /// <summary>
    ///     The DLL ships with the application, so resolving it is a local lookup. Earlier versions
    ///     fetched it from voidtools.com and validated it against a hardcoded hash, which broke every
    ///     installation the moment that archive was rebuilt upstream.
    /// </summary>
    private static async Task<string> EnsureDllAsync()
    {
        if (_everythingDllPath is { Length: > 0 } && File.Exists(_everythingDllPath))
        {
            LoggingService.LogInformation($"Everything SDK DLL already present at '{_everythingDllPath}'.");
            return _everythingDllPath;
        }

        await DllInstallGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_everythingDllPath is { Length: > 0 } existingPath && File.Exists(existingPath))
            {
                LoggingService.LogInformation($"Everything SDK DLL already present at '{existingPath}'.");
                return existingPath;
            }

            if (TryUseLocallyShippedDll(out var localDllPath))
            {
                _everythingDllPath = localDllPath;
                return localDllPath;
            }

            throw new FileNotFoundException(
                $"Bundled {TargetDllFileName} was not found next to the EasyExtract executable.");
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
        // Tolerant sharing flags: another instance may hold the DLL open as a loaded library and
        // File.OpenRead would fail with a sharing violation.
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
        Func<string, bool, bool>? validateDllHashOverride = null,
        Func<IEnumerable<string>>? localProbeDirectoriesOverride = null)
    {
        _ensureDllOverride = ensureDllOverride;
        _registerResolverOverride = registerResolverOverride;
        _loadLibraryOverride = loadLibraryOverride;
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

    internal static bool ValidateDllHashForTests(string dllPath, bool deleteOnMismatch = true)
    {
        return ValidateDllHash(dllPath, deleteOnMismatch);
    }
}
