using EasyExtract.Core.Models;

namespace EasyExtract.Core.Services;

public interface IUnityPackageExtractionService
{
    Task<UnityPackageExtractionResult> ExtractAsync(
        string packagePath,
        string outputDirectory,
        UnityPackageExtractionOptions options,
        IProgress<UnityPackageExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<UnityPackageExtractionResult> ExtractInfoAsync(
        string packagePath,
        CancellationToken cancellationToken = default);
}