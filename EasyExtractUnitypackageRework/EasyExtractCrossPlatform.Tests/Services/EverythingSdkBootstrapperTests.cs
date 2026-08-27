using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class EverythingSdkBootstrapperTests : IDisposable
{
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
    public async Task EnsureDllForTestsAsync_ReusesValidExistingDllWithoutRewrite()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        var root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", Guid.NewGuid().ToString("N"));
        var sdkDirectory = Path.Combine(root, "Install");
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
            validateDllHashOverride: (path, _) =>
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(dllPath), StringComparison.OrdinalIgnoreCase),
            localProbeDirectoriesOverride: () => new[] { sdkDirectory });

        var resolvedPath = await EverythingSdkBootstrapper.EnsureDllForTestsAsync();
        var currentWriteTime = File.GetLastWriteTimeUtc(dllPath);
        var currentBytes = await File.ReadAllBytesAsync(dllPath);

        Assert.Equal(dllPath, resolvedPath, true);
        Assert.Equal(originalWriteTime, currentWriteTime);
        Assert.Equal(originalBytes, currentBytes);

        Directory.Delete(root, true);
    }

    [Fact]
    public async Task EnsureDllForTestsAsync_SerializesConcurrentBundledDllResolution()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        var root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", Guid.NewGuid().ToString("N"));
        var sdkDirectory = Path.Combine(root, "Install");
        Directory.CreateDirectory(sdkDirectory);

        var dllName = Environment.Is64BitProcess ? "Everything64.dll" : "Everything32.dll";
        var dllPath = Path.Combine(sdkDirectory, dllName);
        await File.WriteAllBytesAsync(dllPath, new byte[]
        {
            0xAA, 0xBB, 0xCC
        });

        var validationAttempts = 0;
        EverythingSdkBootstrapper.ConfigureForTests(
            validateDllHashOverride: (path, _) =>
            {
                if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(dllPath),
                        StringComparison.OrdinalIgnoreCase))
                    return true;

                Interlocked.Increment(ref validationAttempts);
                return true;
            },
            localProbeDirectoriesOverride: () => new[] { sdkDirectory });

        var callers = Enumerable.Range(0, 4)
            .Select(_ => EverythingSdkBootstrapper.EnsureDllForTestsAsync())
            .ToArray();

        var resolvedPaths = await Task.WhenAll(callers);

        Assert.All(resolvedPaths, path => Assert.Equal(dllPath, path, true));
        Assert.Equal(1, validationAttempts);

        Directory.Delete(root, true);
    }

    [Fact]
    public async Task EnsureDllForTestsAsync_LeavesUnrelatedTemporaryFilesUntouched()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        var root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", Guid.NewGuid().ToString("N"));
        var sdkDirectory = Path.Combine(root, "Install");
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
            validateDllHashOverride: (path, _) =>
            {
                if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(orphanedTempPath),
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Temporary DLL should not be hash validated.");

                return string.Equals(Path.GetFullPath(path), Path.GetFullPath(dllPath),
                    StringComparison.OrdinalIgnoreCase);
            },
            localProbeDirectoriesOverride: () => new[] { sdkDirectory });

        var resolvedPath = await EverythingSdkBootstrapper.EnsureDllForTestsAsync();

        Assert.Equal(dllPath, resolvedPath, true);
        Assert.True(File.Exists(orphanedTempPath));

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

    /// <summary>
    ///     Without a bundled DLL there is nowhere else to look. Reaching for the network here is what
    ///     broke V2.10.x, so the failure has to stay local and immediate.
    /// </summary>
    [Fact]
    public async Task EnsureDllForTestsAsync_MissingBundledDll_FailsWithoutLookingElsewhere()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();
        EverythingSdkBootstrapper.ConfigureForTests(localProbeDirectoriesOverride: Array.Empty<string>);

        await Assert.ThrowsAsync<FileNotFoundException>(() => EverythingSdkBootstrapper.EnsureDllForTestsAsync());
    }

    [Fact]
    public async Task EnsureDllForTestsAsync_ResolvesTheDllNextToTheExecutable()
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
            0x4D, 0x5A
        });

        EverythingSdkBootstrapper.ConfigureForTests(
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

    /// <summary>
    ///     The bundled DLL and the hash it is checked against both live in this repository, so nothing
    ///     stops the two from drifting apart. Refreshing the SDK without touching ExpectedDllHashes
    ///     would ship a build whose search silently refuses to start, which is the exact failure that
    ///     made the download path untenable in the first place. This is the tripwire for it.
    /// </summary>
    [Fact]
    public void BundledDll_MatchesTheHashTheBootstrapperValidatesAgainst()
    {
        EverythingSdkBootstrapper.ResetForTests();

        var bundledPath = Path.Combine(AppContext.BaseDirectory, "Everything64.dll");

        Assert.True(File.Exists(bundledPath),
            $"The bundled Everything SDK DLL is missing from the build output at '{bundledPath}'.");

        Assert.True(EverythingSdkBootstrapper.ValidateDllHashForTests(bundledPath, false),
            "The bundled Everything64.dll no longer matches its entry in ExpectedDllHashes. " +
            "Update the constant whenever the SDK DLL is refreshed.");
    }
}
