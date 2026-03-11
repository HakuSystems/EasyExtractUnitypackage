using EasyExtract.Core.Services;
using EasyExtractCrossPlatform.Tests.Support;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class UnityPackagePreviewServiceTests
{
    [Fact]
    public async Task LoadPreviewAsync_InvalidFormat_LogsWarningInsteadOfError()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "EasyExtract.Tests", Guid.NewGuid().ToString("N"));
        var packagePath = Path.Combine(tempRoot, "invalid.txt");
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(packagePath, "not a unitypackage");

        var logger = new RecordingLogger();
        var service = new UnityPackagePreviewService(logger);

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadPreviewAsync(packagePath));

            Assert.NotEmpty(logger.WarningEntries);
            Assert.Empty(logger.ErrorEntries);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }
}