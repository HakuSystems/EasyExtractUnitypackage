using EasyExtract.Core.Models;
using EasyExtract.Core.Utilities;

namespace EasyExtract.Core.Services;

public sealed partial class UnityPackageExtractionService : IUnityPackageExtractionService
{
    private const int StreamCopyBufferSize = 128 * 1024;
    private const int MaxPathEntryCharacters = 4096;

    // Path.GetInvalidFileNameChars() is platform-dependent (on Linux it is only
    // '/' and NUL), so the desktop app and the Linux-hosted HakuApi would
    // normalize the same archive differently. Use the Windows superset on every
    // platform so output paths and collision detection stay identical.
    private static readonly HashSet<char> InvalidFileNameCharacters =
        BuildInvalidFileNameCharacters();

    private static HashSet<char> BuildInvalidFileNameCharacters()
    {
        var characters = new HashSet<char>(Path.GetInvalidFileNameChars())
        {
            '"', '<', '>', '|', ':', '*', '?', '\\', '/'
        };

        for (var c = '\0'; c <= '\u001f'; c++)
            characters.Add(c);

        return characters;
    }

    private static readonly PathSegmentNormalization[] EmptySegmentNormalizations =
        Array.Empty<PathSegmentNormalization>();

    private readonly IEasyExtractLogger _logger;

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
        outputDirectory =
            NormalizeAndValidateDirectoryRoot(outputDirectory, "Output directory", correlationId, _logger);

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

        var packageSize = new FileInfo(packagePath).Length;
        var requiredSpace = packageSize * 3;
        var freeSpace = DiskSpaceHelper.GetFreeSpace(outputDirectory);

        if (freeSpace < requiredSpace)
        {
            var message = DiskSpaceHelper.BuildFriendlyMessage(outputDirectory);
            // 0x80070070 is HR_DISK_FULL
            throw new IOException(message, unchecked((int)0x80070070));
        }

        // The package is a compressed archive, so its file size is a lower bound for the
        // extracted volume. Failing here beats aborting hours into the extraction.
        if (limits.MaxPackageBytes > 0 && packageSize > limits.MaxPackageBytes)
        {
            _logger.LogWarning(
                $"ExtractAsync aborted early: package exceeds extraction budget | package='{packagePath}' | size={packageSize:N0} | limit={limits.MaxPackageBytes:N0} | correlationId={correlationId}");
            throw new InvalidDataException(
                $"Extraction aborted. The package file is {packageSize:N0} bytes and already exceeds the configured extraction limit of {limits.MaxPackageBytes:N0} bytes. Increase the limit in the settings to extract this package.");
        }

        // HakuAPI Logic: Ensure Isolation if TemporaryDirectory is provided (or if we need one internally)
        // If the user (or API) provides a general temp root, we append a session-ID based folder to it to avoid collisions.
        // If options.TemporaryDirectory is null, we stick to null (Internal logic might use default temp if needed, but here we prep custom path).
        var activeTempDir = options.TemporaryDirectory;
        if (!string.IsNullOrWhiteSpace(activeTempDir))
        {
            activeTempDir = NormalizeAndValidateDirectoryRoot(activeTempDir, "Temporary directory root", correlationId,
                _logger);

            // We ensure we don't just dump into the root of provided temp, but a specific subfolder if not already unique.
            // But usually the desktop client provides a unique folder.
            // For API, the caller should likely handle uniqueness or we enforce it here.
            // To be safe and compliant with "Isolation" request:
            if (!activeTempDir.Contains(correlationId))
                // Append correlation ID to ensure isolation if not present
                activeTempDir = Path.Combine(activeTempDir, $"EasyExtract_Session_{correlationId}");

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
        var activeOptions = options with
        {
            TemporaryDirectory = activeTempDir
        };

        using (_logger.BeginPerformanceScope("UnityPackageExtraction", "Extraction", correlationId))
        {
            try
            {
                _logger.LogMemoryUsage($"Before extraction | correlationId={correlationId}");

                var result = await ExtractInternalAsync(
                        packagePath, outputDirectory, activeOptions, progress, cancellationToken,
                        correlationId)
                    .ConfigureAwait(false);

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
            catch (ExtractionSecurityException ex)
            {
                _logger.LogWarning(
                    $"ExtractAsync aborted: security validation failed | package='{packagePath}' | correlationId={correlationId}",
                    ex);
                throw new InvalidDataException(ex.Message, ex);
            }
            catch (InvalidDataException ex)
            {
                _logger.LogWarning(
                    $"ExtractAsync aborted: invalid package data | package='{packagePath}' | correlationId={correlationId}",
                    ex);
                throw;
            }
            catch (IOException ex) when (IsFileLockContention(ex))
            {
                // Typically Unity or a download manager still holds the .unitypackage.
                _logger.LogWarning(
                    $"ExtractAsync aborted: package file is locked by another process | package='{packagePath}' | correlationId={correlationId}",
                    ex);
                throw new IOException(
                    "The package file is currently in use by another program (for example Unity is still downloading it). Wait until it is finished or close the other program, then try again.",
                    ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"ExtractAsync failed | package='{packagePath}' | correlationId={correlationId}", ex);
                throw;
            }
        }
    }

    private static bool IsFileLockContention(IOException ex)
    {
        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);
        return ex.HResult == sharingViolation || ex.HResult == lockViolation;
    }
}