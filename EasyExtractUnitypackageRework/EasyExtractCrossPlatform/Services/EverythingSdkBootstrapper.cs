using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace EasyExtractCrossPlatform.Services;

internal static class EverythingSdkBootstrapper
{
    private static readonly Uri SdkDownloadUri = new("https://www.voidtools.com/Everything-SDK.zip");
    private static readonly HttpClient HttpClient = new();
    private static readonly object InitializationLock = new();

    private static readonly string TargetDllFileName =
        Environment.Is64BitProcess ? "Everything64.dll" : "Everything32.dll";

    private static Task? _initializationTask;
    private static string? _everythingDllPath;
    private static IntPtr _libraryHandle = IntPtr.Zero;
    private static bool _resolverRegistered;

    public static Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            return Task.CompletedTask;

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
            var dllPath = await EnsureDllAsync().ConfigureAwait(false);
            RegisterDllImportResolver(dllPath);
            LoadLibrary(dllPath);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException
                                       or NotSupportedException)
        {
            throw EverythingSearchException.MissingLibrary(ex);
        }
        catch (Exception ex) when (ex is not EverythingSearchException)
        {
            throw EverythingSearchException.NativeFailure(ex);
        }
    }

    private static async Task<string> EnsureDllAsync()
    {
        if (_everythingDllPath is { Length: > 0 } && File.Exists(_everythingDllPath))
            return _everythingDllPath;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sdkDirectory = Path.Combine(appData, "EasyExtract", "ThirdParty", "EverythingSdk");
        Directory.CreateDirectory(sdkDirectory);

        var dllPath = Path.Combine(sdkDirectory, TargetDllFileName);
        if (File.Exists(dllPath))
        {
            _everythingDllPath = dllPath;
            return dllPath;
        }

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"Everything-SDK-{Guid.NewGuid():N}.zip");

        try
        {
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

            using var archive = ZipFile.OpenRead(tempZipPath);
            var dllEntry = archive.Entries
                .FirstOrDefault(entry =>
                    entry.FullName.EndsWith(TargetDllFileName, StringComparison.OrdinalIgnoreCase));

            if (dllEntry is null)
                throw new InvalidOperationException($"The Everything SDK package did not contain {TargetDllFileName}.");

            await using var entryStream = dllEntry.Open();
            await using var destinationStream = File.Create(dllPath);
            await entryStream.CopyToAsync(destinationStream).ConfigureAwait(false);
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
    }
}