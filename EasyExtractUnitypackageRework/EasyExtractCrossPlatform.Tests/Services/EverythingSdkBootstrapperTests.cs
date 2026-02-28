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
    public async Task PromoteStagedDllForTestsAsync_RetriesSharingViolationAndSucceeds()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EverythingSdkBootstrapper.ResetForTests();

        var root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var destinationPath = Path.Combine(root, "Everything64.dll");
        var stagedPath = Path.Combine(root, "Everything64.dll.stage");
        var expectedBytes = new byte[] { 0x11, 0x22, 0x33, 0x44 };

        await File.WriteAllBytesAsync(stagedPath, expectedBytes);
        await File.WriteAllBytesAsync(destinationPath, new byte[] { 0x00, 0x00, 0x00, 0x00 });

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
        var originalBytes = new byte[] { 0x10, 0x20, 0x30 };

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
}