using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyExtract.Core.Models;

namespace EasyExtract.Core.Services;


public sealed partial class UnityPackageExtractionService : IUnityPackageExtractionService
{
    private readonly IEasyExtractLogger _logger;
    private const int StreamCopyBufferSize = 128 * 1024;
    private const int MaxPathEntryCharacters = 4096;

    private static readonly HashSet<char> InvalidFileNameCharacters =
        Path.GetInvalidFileNameChars().ToHashSet();

    private static readonly PathSegmentNormalization[] EmptySegmentNormalizations =
        Array.Empty<PathSegmentNormalization>();

    public UnityPackageExtractionService(IEasyExtractLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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

        _logger.LogInformation(
            $"ExtractAsync: package='{packagePath}' | output='{outputDirectory}' | organize={options.OrganizeByCategories} | temp='{options.TemporaryDirectory}' | limits=[maxAssets={limits.MaxAssets}, maxAssetBytes={limits.MaxAssetBytes:N0}, maxPackageBytes={limits.MaxPackageBytes:N0}] | correlationId={correlationId}");

        if (!File.Exists(packagePath))
        {
            _logger.LogError(
                $"ExtractAsync aborted: File not found | path='{packagePath}' | correlationId={correlationId}");
            throw new FileNotFoundException("Unitypackage file was not found.", packagePath);
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
            _logger.LogInformation(
                $"Created output directory | path='{outputDirectory}' | correlationId={correlationId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"Failed to create output directory | path='{outputDirectory}' | correlationId={correlationId}", ex);
            throw;
        }

        // HakuAPI Logic: Ensure Isolation if TemporaryDirectory is provided (or if we need one internally)
        // If the user (or API) provides a general temp root, we append a session-ID based folder to it to avoid collisions.
        // If options.TemporaryDirectory is null, we stick to null (Internal logic might use default temp if needed, but here we prep custom path).
        string? activeTempDir = options.TemporaryDirectory;
        if (!string.IsNullOrWhiteSpace(activeTempDir))
        {
             // We ensure we don't just dump into the root of provided temp, but a specific subfolder if not already unique.
             // But usually the desktop client provides a unique folder.
             // For API, the caller should likely handle uniqueness or we enforce it here.
             // To be safe and compliant with "Isolation" request:
             if (!activeTempDir.Contains(correlationId)) 
             {
                 // Append correlation ID to ensure isolation if not present
                 activeTempDir = Path.Combine(activeTempDir, $"EasyExtract_Session_{correlationId}");
             }

            try
            {
                Directory.CreateDirectory(activeTempDir);
                _logger.LogInformation(
                    $"Created temporary directory | path='{activeTempDir}' | correlationId={correlationId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Failed to create temporary directory | path='{activeTempDir}' | correlationId={correlationId}",
                    ex);
                throw;
            }
        }
        
        // Use modified options for internal call
        var activeOptions = options with { TemporaryDirectory = activeTempDir };

        using (_logger.BeginPerformanceScope("UnityPackageExtraction", "Extraction", correlationId))
        {
            try
            {
                _logger.LogMemoryUsage($"Before extraction | correlationId={correlationId}");

                var result = await Task.Run(() =>
                        ExtractInternal(packagePath, outputDirectory, activeOptions, progress, cancellationToken,
                            correlationId),
                    cancellationToken).ConfigureAwait(false);

                _logger.LogMemoryUsage($"After extraction | correlationId={correlationId}", true);
                _logger.LogInformation(
                    $"ExtractAsync completed: assets={result.AssetsExtracted} | files={result.ExtractedFiles.Count} | size={result.TotalSize:N0} | output='{result.OutputDirectory}' | correlationId={correlationId}");
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(
                    $"ExtractAsync cancelled | package='{packagePath}' | correlationId={correlationId}");
                throw;
            }
            catch (InvalidDataException ex)
            {
                _logger.LogError(
                    $"ExtractAsync aborted: invalid package data | package='{packagePath}' | correlationId={correlationId}",
                    ex);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"ExtractAsync failed | package='{packagePath}' | correlationId={correlationId}", ex);
                throw;
            }
        }
    }
}
