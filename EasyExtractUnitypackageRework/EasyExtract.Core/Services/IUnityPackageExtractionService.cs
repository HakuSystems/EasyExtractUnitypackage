using System;
using System.Threading;
using System.Threading.Tasks;
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
}
