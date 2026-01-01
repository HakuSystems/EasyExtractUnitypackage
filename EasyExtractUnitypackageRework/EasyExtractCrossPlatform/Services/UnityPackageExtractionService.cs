namespace EasyExtractCrossPlatform.Services;

public interface IUnityPackageExtractionService
{
    Task<UnityPackageExtractionResult> ExtractAsync(
        string packagePath,
        string outputDirectory,
        UnityPackageExtractionOptions options,
        IProgress<UnityPackageExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record UnityPackageExtractionOptions(
    bool OrganizeByCategories,
    string? TemporaryDirectory,
    UnityPackageExtractionLimits? Limits = null);

public sealed record UnityPackageExtractionProgress(string? AssetPath, int AssetsExtracted);

public sealed record UnityPackageExtractionResult(
    string PackagePath,
    string OutputDirectory,
    int AssetsExtracted,
    IReadOnlyList<string> ExtractedFiles);

public sealed partial class UnityPackageExtractionService : IUnityPackageExtractionService
{
    private const int StreamCopyBufferSize = 128 * 1024;
    private const int MaxPathEntryCharacters = 4096;

    private static readonly HashSet<char> InvalidFileNameCharacters =
        Path.GetInvalidFileNameChars().ToHashSet();

    private static readonly PathSegmentNormalization[] EmptySegmentNormalizations =
        Array.Empty<PathSegmentNormalization>();

    public async Task<UnityPackageExtractionResult> ExtractAsync(
        string packagePath,
        string outputDirectory,
        UnityPackageExtractionOptions options,
        IProgress<UnityPackageExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var correlationId = Guid.NewGuid().ToString("N")[..8];
        var limits = UnityPackageExtractionLimits.Normalize(options.Limits);

        LoggingService.LogInformation(
            $"ExtractAsync: package='{packagePath}' | output='{outputDirectory}' | organize={options.OrganizeByCategories} | temp='{options.TemporaryDirectory}' | limits=[maxAssets={limits.MaxAssets}, maxAssetBytes={limits.MaxAssetBytes:N0}, maxPackageBytes={limits.MaxPackageBytes:N0}] | correlationId={correlationId}");

        if (!File.Exists(packagePath))
        {
            LoggingService.LogError(
                $"ExtractAsync aborted: File not found | path='{packagePath}' | correlationId={correlationId}",
                forwardToWebhook: false);
            throw new FileNotFoundException("Unitypackage file was not found.", packagePath);
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
            LoggingService.LogInformation(
                $"Created output directory | path='{outputDirectory}' | correlationId={correlationId}");
        }
        catch (Exception ex)
        {
            LoggingService.LogError(
                $"Failed to create output directory | path='{outputDirectory}' | correlationId={correlationId}", ex);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(options.TemporaryDirectory))
            try
            {
                Directory.CreateDirectory(options.TemporaryDirectory!);
                LoggingService.LogInformation(
                    $"Created temporary directory | path='{options.TemporaryDirectory}' | correlationId={correlationId}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError(
                    $"Failed to create temporary directory | path='{options.TemporaryDirectory}' | correlationId={correlationId}",
                    ex);
                throw;
            }

        using (LoggingService.BeginPerformanceScope("UnityPackageExtraction", "Extraction",
                   correlationId))
        {
            try
            {
                LoggingService.LogMemoryUsage($"Before extraction | correlationId={correlationId}");

                var result = await Task.Run(() =>
                        ExtractInternal(packagePath, outputDirectory, options, progress, cancellationToken,
                            correlationId),
                    cancellationToken).ConfigureAwait(false);

                LoggingService.LogMemoryUsage($"After extraction | correlationId={correlationId}",
                    true);
                LoggingService.LogInformation(
                    $"ExtractAsync completed: assets={result.AssetsExtracted} | files={result.ExtractedFiles.Count} | output='{result.OutputDirectory}' | correlationId={correlationId}");
                return result;
            }
            catch (OperationCanceledException)
            {
                LoggingService.LogInformation(
                    $"ExtractAsync cancelled | package='{packagePath}' | correlationId={correlationId}");
                throw;
            }
            catch (InvalidDataException ex)
            {
                LoggingService.LogError(
                    $"ExtractAsync aborted: invalid package data | package='{packagePath}' | correlationId={correlationId}",
                    ex,
                    false);
                throw;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(
                    $"ExtractAsync failed | package='{packagePath}' | correlationId={correlationId}", ex);
                throw;
            }
        }
    }
}