using System.Buffers;
using EasyExtract.Core.Models;

namespace EasyExtract.Core.Services;

public sealed partial class UnityPackageExtractionService
{
    private readonly record struct AssetWritePlan(bool WriteAsset, bool WriteMeta, bool WritePreview)
    {
        public bool RequiresWrite => WriteAsset || WriteMeta || WritePreview;
    }


    private sealed record PendingAssetWrite(
        UnityPackageAssetState State,
        string TargetPath,
        string? MetaPath,
        string? PreviewPath,
        AssetWritePlan Plan);


    private sealed class ExtractionLimiter
    {
        private int _assetCount;
        private long _totalBytes;

        public ExtractionLimiter(UnityPackageExtractionLimits limits)
        {
            Limits = limits;
        }

        public UnityPackageExtractionLimits Limits { get; }

        public void ValidateDeclaredSize(long declaredLength, string entryName)
        {
            if (declaredLength <= 0 || Limits.MaxAssetBytes <= 0)
                return;

            if (declaredLength > Limits.MaxAssetBytes)
                throw new InvalidDataException(
                    $"Entry '{entryName}' declares {declaredLength:N0} bytes which exceeds the per-file limit of {Limits.MaxAssetBytes:N0} bytes.");
        }

        public void TrackAssetBytes(long bytes)
        {
            if (bytes <= 0)
                return;

            if (Limits.MaxAssetBytes > 0 && bytes > Limits.MaxAssetBytes)
                throw new InvalidDataException(
                    $"Asset exceeded the per-file limit of {Limits.MaxAssetBytes:N0} bytes.");

            if (Limits.MaxPackageBytes <= 0)
                return;

            if (long.MaxValue - _totalBytes < bytes)
                throw new InvalidDataException("Extraction aborted due to overflow while tracking package size.");

            var next = _totalBytes + bytes;
            if (next > Limits.MaxPackageBytes)
                throw new InvalidDataException(
                    $"Extraction aborted. Total extracted bytes {next:N0} exceeded the configured limit of {Limits.MaxPackageBytes:N0} bytes.");

            _totalBytes = next;
        }

        public void RegisterAsset()
        {
            if (Limits.MaxAssets <= 0)
                return;

            if (_assetCount + 1 > Limits.MaxAssets)
                throw new InvalidDataException(
                    $"Extraction aborted. Asset count exceeded the configured limit of {Limits.MaxAssets:N0} entries.");

            _assetCount++;
        }
    }

    private readonly record struct PathSegmentNormalization(string Original, string Normalized);

    private readonly record struct PathNormalizationResult(
        string NormalizedPath,
        string OriginalPath,
        IReadOnlyList<PathSegmentNormalization> Segments);

    private sealed class UnityPackageAssetState
    {
        private AssetComponent? _asset;
        private AssetComponent? _meta;
        private AssetComponent? _preview;
        public string? RelativePath { get; set; }
        public string? OriginalRelativePath { get; set; }
        public IReadOnlyList<PathSegmentNormalization>? PathNormalizations { get; set; }

        public AssetComponent? Asset => _asset;
        public AssetComponent? Meta => _meta;
        public AssetComponent? Preview => _preview;
        private bool Completed { get; set; }

        public bool CanWriteToDisk =>
            !Completed &&
            !string.IsNullOrWhiteSpace(RelativePath) &&
            Asset is { HasContent: true };

        public void SetAssetComponent(AssetComponent? component)
        {
            ReplaceComponent(ref _asset, component);
        }

        public void SetMetaComponent(AssetComponent? component)
        {
            ReplaceComponent(ref _meta, component);
        }

        public void SetPreviewComponent(AssetComponent? component)
        {
            ReplaceComponent(ref _preview, component);
        }

        public void MarkAsCompleted()
        {
            RelativePath = null;
            OriginalRelativePath = null;
            PathNormalizations = null;
            ReplaceComponent(ref _asset, null);
            ReplaceComponent(ref _meta, null);
            ReplaceComponent(ref _preview, null);
            Completed = true;
        }

        private void ReplaceComponent(ref AssetComponent? target, AssetComponent? value)
        {
            if (ReferenceEquals(target, value))
                return;

            if (Completed)
            {
                value?.Dispose();
                return;
            }

            target?.Dispose();
            target = value;
        }
    }


    private sealed class TemporaryDirectoryScope : IDisposable
    {
        private readonly IEasyExtractLogger _logger;

        public TemporaryDirectoryScope(string directoryPath, string correlationId, IEasyExtractLogger logger)
        {
            DirectoryPath = directoryPath;
            CorrelationId = correlationId;
            _logger = logger;
        }

        public string DirectoryPath { get; }

        public string CorrelationId { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                    Directory.Delete(DirectoryPath, true);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(
                    $"Failed to delete empty directory | path='{DirectoryPath}' | correlationId={CorrelationId}",
                    ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    $"Access denied when deleting directory | path='{DirectoryPath}' | correlationId={CorrelationId}",
                    ex);
            }
        }
    }


    private sealed class AssetComponent : IDisposable
    {
        private readonly IEasyExtractLogger _logger;
        private bool _disposed;

        public AssetComponent(string tempPath, long length, byte[] contentHash, IEasyExtractLogger logger)
        {
            TempPath = tempPath;
            Length = length;
            ContentHash = contentHash;
            _logger = logger;
        }

        public string TempPath { get; }
        public long Length { get; }
        public ReadOnlyMemory<byte> ContentHash { get; }
        public bool HasContent => Length >= 0;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            TryDeleteFile(TempPath, _logger);
        }

        public void CopyTo(string destinationPath, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AssetComponent));

            cancellationToken.ThrowIfCancellationRequested();

            using var source = File.Open(TempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destination = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);

            try
            {
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    destination.Write(buffer, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}