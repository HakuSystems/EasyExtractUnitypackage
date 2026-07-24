using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class EverythingSdkBootstrapperTests : IDisposable
{
    private const int SharingViolationHResult = unchecked((int)0x80070020);

    public void Dispose()
    {
        EverythingSdkBootstrapper.ResetForTests();
    }

    [Fact]
    public async Task EnsureInitializedAsync_RetriesAfterFaultedInitializationTask()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        var attempts = 0;
        EverythingSdkBootstrapper.ConfigureForTests(
            () =>
            {
                attempts++;
                if (attempts == 1)
                    throw new IOException("simulated failure");
                return Task.FromResult(@"C:\test\Everything64.dll");
            },
            _ => { },
            _ => { });

        await Assert.ThrowsAsync<EverythingSearchException>(() => EverythingSdkBootstrapper.EnsureInitializedAsync());

        await EverythingSdkBootstrapper.EnsureInitializedAsync();

        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task PromoteStagedDllForTestsAsync_RetriesSharingViolationAndSucceeds()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        var root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var destinationPath = Path.Combine(root, "Everything64.dll");
        var stagedPath = Path.Combine(root, "Everything64.dll.stage");
        var expectedBytes = new byte[]
        {
            0x11, 0x22, 0x33, 0x44
        };

        await File.WriteAllBytesAsync(stagedPath, expectedBytes);
        await File.WriteAllBytesAsync(destinationPath, new byte[]
        {
            0x00, 0x00, 0x00, 0x00
        });

        var lockStream = new FileStream(
            destinationPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        try
        {
            var releaseTask = Task.Run(async () =>
            {
                await Task.Delay(120);
                lockStream.Dispose();
            });

            await EverythingSdkBootstrapper.PromoteStagedDllForTestsAsync(
                stagedPath,
                destinationPath,
                8,
                TimeSpan.FromMilliseconds(25));

            await releaseTask;
        }
        finally
        {
            lockStream.Dispose();
        }

        var actualBytes = await File.ReadAllBytesAsync(destinationPath);
        Assert.Equal(expectedBytes, actualBytes);
        Assert.False(File.Exists(stagedPath));

        Directory.Delete(root, true);
    }

    [Fact]
    public async Task EnsureDllForTestsAsync_ReusesValidExistingDllWithoutRewrite()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        var root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", Guid.NewGuid().ToString("N"));
        var appDataPath = Path.Combine(root, "AppData");
        var sdkDirectory = Path.Combine(appDataPath, "EasyExtract", "ThirdParty", "EverythingSdk");
        Directory.CreateDirectory(sdkDirectory);

        var dllName = Environment.Is64BitProcess ? "Everything64.dll" : "Everything32.dll";
        var dllPath = Path.Combine(sdkDirectory, dllName);
        var originalBytes = new byte[]
        {
            0x10, 0x20, 0x30
        };

        await File.WriteAllBytesAsync(dllPath, originalBytes);
        var originalWriteTime = File.GetLastWriteTimeUtc(dllPath);

        EverythingSdkBootstrapper.ConfigureForTests(
            appDataPathOverride: () => appDataPath,
            validateDllHashOverride: (path, _) =>
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(dllPath), StringComparison.OrdinalIgnoreCase));

        var resolvedPath = await EverythingSdkBootstrapper.EnsureDllForTestsAsync();
        var currentWriteTime = File.GetLastWriteTimeUtc(dllPath);
        var currentBytes = await File.ReadAllBytesAsync(dllPath);

        Assert.Equal(dllPath, resolvedPath, true);
        Assert.Equal(originalWriteTime, currentWriteTime);
        Assert.Equal(originalBytes, currentBytes);

        Directory.Delete(root, true);
    }

    [Fact]
    public async Task EnsureDllForTestsAsync_SerializesConcurrentReuseWhileValidationRetries()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        var root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", Guid.NewGuid().ToString("N"));
        var appDataPath = Path.Combine(root, "AppData");
        var sdkDirectory = Path.Combine(appDataPath, "EasyExtract", "ThirdParty", "EverythingSdk");
        Directory.CreateDirectory(sdkDirectory);

        var dllName = Environment.Is64BitProcess ? "Everything64.dll" : "Everything32.dll";
        var dllPath = Path.Combine(sdkDirectory, dllName);
        await File.WriteAllBytesAsync(dllPath, new byte[]
        {
            0xAA, 0xBB, 0xCC
        });

        var validationAttempts = 0;
        EverythingSdkBootstrapper.ConfigureForTests(
            appDataPathOverride: () => appDataPath,
            validateDllHashOverride: (path, _) =>
            {
                if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(dllPath),
                        StringComparison.OrdinalIgnoreCase))
                    return true;

                if (Interlocked.Increment(ref validationAttempts) == 1)
                    throw new SharingViolationIOException();

                return true;
            });

        var callers = Enumerable.Range(0, 4)
            .Select(_ => EverythingSdkBootstrapper.EnsureDllForTestsAsync())
            .ToArray();

        var resolvedPaths = await Task.WhenAll(callers);

        Assert.All(resolvedPaths, path => Assert.Equal(dllPath, path, true));
        Assert.True(validationAttempts >= 2);

        Directory.Delete(root, true);
    }

    [Fact]
    public async Task EnsureDllForTestsAsync_CleansOrphanedTemporaryDlls()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        var root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", Guid.NewGuid().ToString("N"));
        var appDataPath = Path.Combine(root, "AppData");
        var sdkDirectory = Path.Combine(appDataPath, "EasyExtract", "ThirdParty", "EverythingSdk");
        Directory.CreateDirectory(sdkDirectory);

        var dllName = Environment.Is64BitProcess ? "Everything64.dll" : "Everything32.dll";
        var dllPath = Path.Combine(sdkDirectory, dllName);
        var orphanedTempPath = Path.Combine(sdkDirectory, $"{dllName}.{Guid.NewGuid():N}.tmp");

        await File.WriteAllBytesAsync(dllPath, new byte[]
        {
            0xAA, 0xBB, 0xCC
        });
        await File.WriteAllBytesAsync(orphanedTempPath, new byte[]
        {
            0x10, 0x20, 0x30
        });

        EverythingSdkBootstrapper.ConfigureForTests(
            appDataPathOverride: () => appDataPath,
            validateDllHashOverride: (path, _) =>
            {
                if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(orphanedTempPath),
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Temporary DLL should not be hash validated.");

                return string.Equals(Path.GetFullPath(path), Path.GetFullPath(dllPath),
                    StringComparison.OrdinalIgnoreCase);
            });

        var resolvedPath = await EverythingSdkBootstrapper.EnsureDllForTestsAsync();

        Assert.Equal(dllPath, resolvedPath, true);
        Assert.False(File.Exists(orphanedTempPath));

        Directory.Delete(root, true);
    }

    [Fact]
    public async Task ValidateDllHashForTests_TemporarySdkFile_DoesNotThrowAndDeletesFile()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        var root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var dllName = Environment.Is64BitProcess ? "Everything64.dll" : "Everything32.dll";
        var tempDllPath = Path.Combine(root, $"{dllName}.{Guid.NewGuid():N}.tmp");
        await File.WriteAllBytesAsync(tempDllPath, new byte[]
        {
            0x01, 0x02, 0x03
        });

        var isValid = EverythingSdkBootstrapper.ValidateDllHashForTests(tempDllPath);

        Assert.False(isValid);
        Assert.False(File.Exists(tempDllPath));

        Directory.Delete(root, true);
    }

    [Fact]
    public async Task EnsureInitializedAsync_TruncatedDownload_ReportsServiceUnavailableInsteadOfMissingLibrary()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        EverythingSdkBootstrapper.ConfigureForTests(
            () => throw new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely."),
            _ => { },
            _ => { });

        var exception = await Assert.ThrowsAsync<EverythingSearchException>(
            () => EverythingSdkBootstrapper.EnsureInitializedAsync());

        Assert.Equal(EverythingErrorCode.ServiceUnavailable, exception.ErrorCode);
        Assert.DoesNotContain("was not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(typeof(InvalidDataException), true)]
    [InlineData(typeof(TaskCanceledException), true)]
    [InlineData(typeof(TimeoutException), true)]
    [InlineData(typeof(InvalidOperationException), false)]
    [InlineData(typeof(UnauthorizedAccessException), false)]
    public void IsTransientDownloadFailure_ClassifiesRetryableFailures(Type exceptionType, bool expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        Assert.Equal(expected, EverythingSdkBootstrapper.IsTransientDownloadFailure(exception));
    }

    [Fact]
    public void IsTransientDownloadFailure_TruncatedHttpResponse_IsRetryable()
    {
        var exception = new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely.");

        Assert.True(EverythingSdkBootstrapper.IsTransientDownloadFailure(exception));
    }

    [Fact]
    public async Task EnsureDllForTestsAsync_FallsBackToDllNextToTheExecutable()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        var root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", Guid.NewGuid().ToString("N"));
        var appDataPath = Path.Combine(root, "AppData");
        var installDirectory = Path.Combine(root, "Install");
        Directory.CreateDirectory(installDirectory);

        var dllName = Environment.Is64BitProcess ? "Everything64.dll" : "Everything32.dll";
        var localDllPath = Path.Combine(installDirectory, dllName);
        await File.WriteAllBytesAsync(localDllPath, new byte[]
        {
            0x4D, 0x5A
        });

        EverythingSdkBootstrapper.ConfigureForTests(
            appDataPathOverride: () => appDataPath,
            validateDllHashOverride: (path, _) =>
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(localDllPath),
                    StringComparison.OrdinalIgnoreCase),
            localProbeDirectoriesOverride: () => new[] { installDirectory });

        var resolvedPath = await EverythingSdkBootstrapper.EnsureDllForTestsAsync();

        Assert.Equal(localDllPath, resolvedPath, true);
        Assert.True(File.Exists(localDllPath));

        Directory.Delete(root, true);
    }

    [Fact]
    public async Task TryUseLocallyShippedDllForTests_BadHash_IsIgnoredAndTheFileIsKept()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        var root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", Guid.NewGuid().ToString("N"));
        var installDirectory = Path.Combine(root, "Install");
        Directory.CreateDirectory(installDirectory);

        var dllName = Environment.Is64BitProcess ? "Everything64.dll" : "Everything32.dll";
        var localDllPath = Path.Combine(installDirectory, dllName);
        await File.WriteAllBytesAsync(localDllPath, new byte[]
        {
            0xDE, 0xAD
        });

        EverythingSdkBootstrapper.ConfigureForTests(
            validateDllHashOverride: (_, deleteOnMismatch) =>
            {
                Assert.False(deleteOnMismatch, "A user supplied DLL must never be deleted.");
                return false;
            },
            localProbeDirectoriesOverride: () => new[] { installDirectory });

        var resolved = EverythingSdkBootstrapper.TryUseLocallyShippedDllForTests(out _);

        Assert.False(resolved);
        Assert.True(File.Exists(localDllPath));

        Directory.Delete(root, true);
    }

    private sealed class SharingViolationIOException : IOException
    {
        public SharingViolationIOException() : base("Simulated sharing violation")
        {
            HResult = SharingViolationHResult;
        }
    }
}