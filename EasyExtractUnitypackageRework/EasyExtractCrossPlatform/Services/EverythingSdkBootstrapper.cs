using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace EasyExtractCrossPlatform.Services;

internal static class EverythingSdkBootstrapper
{
    private static readonly Uri SdkDownloadUri = new("https://www.voidtools.com/Everything-SDK.zip");
    private static readonly HttpClient HttpClient = new();
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
            initTask = _initializationTask ??= InitializeAsync();
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
            var dllPath = await EnsureDllAsync().ConfigureAwait(false);
            RegisterDllImportResolver(dllPath);
            LoadLibrary(dllPath);
            LoggingService.LogInformation($"Everything SDK initialized using '{dllPath}'.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException
                                       or NotSupportedException)
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

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sdkDirectory = Path.Combine(appData, "EasyExtract", "ThirdParty", "EverythingSdk");
        Directory.CreateDirectory(sdkDirectory);

        var dllPath = Path.Combine(sdkDirectory, TargetDllFileName);
        if (File.Exists(dllPath))
        {
            if (ValidateDllHash(dllPath))
            {
                LoggingService.LogInformation($"Reusing previously downloaded Everything SDK DLL at '{dllPath}'.");
                _everythingDllPath = dllPath;
                return dllPath;
            }

            LoggingService.LogInformation($"Discarding Everything SDK DLL at '{dllPath}' due to hash mismatch.");
        }

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"Everything-SDK-{Guid.NewGuid():N}.zip");

        try
        {
            LoggingService.LogInformation($"Downloading Everything SDK from '{SdkDownloadUri}'.");
            using (var response = await HttpClient.GetAsync(SdkDownloadUri, HttpCompletionOption.ResponseHeadersRead)
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
                throw new InvalidOperationException($"The Everything SDK package did not contain {TargetDllFileName}.");

            await using (var entryStream = dllEntry.Open())
            await using (var destinationStream = File.Create(dllPath))
            {
                await entryStream.CopyToAsync(destinationStream).ConfigureAwait(false);
            }

            if (!ValidateDllHash(dllPath))
            {
                LoggingService.LogError("The downloaded Everything SDK DLL failed integrity validation.");
                throw new InvalidOperationException("The downloaded Everything SDK DLL failed integrity validation.");
            }

            LoggingService.LogInformation($"Everything SDK DLL extracted to '{dllPath}'.");
        }
        finally
        {
            try
            {
                if (File.Exists(tempZipPath))
                    File.Delete(tempZipPath);
            }
            catch
            {
                // ignored
            }
        }

        _everythingDllPath = dllPath;
        return dllPath;
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

    private static bool ValidateDllHash(string dllPath)
    {
        if (!ExpectedDllHashes.TryGetValue(Path.GetFileName(dllPath), out var expectedHash))
            throw new InvalidOperationException($"No expected hash registered for {Path.GetFileName(dllPath)}.");

        string actualHash;
        using (var stream = File.OpenRead(dllPath))
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(stream);
            actualHash = Convert.ToHexString(hashBytes);
        }

        if (actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            return true;

        File.Delete(dllPath);
        LoggingService.LogError(
            $"Everything SDK DLL at '{dllPath}' failed validation. Expected {expectedHash}, actual {actualHash}. Deleted file.");
        return false;
    }
}