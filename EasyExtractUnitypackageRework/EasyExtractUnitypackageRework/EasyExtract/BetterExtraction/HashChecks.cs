using System.Security.Cryptography;
using System.Text;

namespace EasyExtract.BetterExtraction;

public static class HashChecks
{
    /// <summary>
    /// Computes an MD5 hash for a file (legacy method)
    /// </summary>
    /// <param name="file">The file to hash</param>
    /// <returns>The MD5 hash as a hexadecimal string</returns>
    public static string ComputeFileHash(FileInfo file)
    {
        using var stream = file.OpenRead();
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(stream);
        var sb = new StringBuilder();
        foreach (var b in hashBytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
    
    /// <summary>
    /// Computes multiple hash values for a file asynchronously
    /// </summary>
    /// <param name="file">The file to hash</param>
    /// <param name="algorithms">The algorithms to use, or null to compute all hashes</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <returns>A FileHashInfo object containing the computed hashes</returns>
    public static async Task<FileHashInfo> ComputeMultipleHashesAsync(
        FileInfo file,
        HashAlgorithmType[]? algorithms = null,
        IProgress<double>? progress = null)
    {
        // If no algorithms specified, compute all
        algorithms ??= new[] 
        { 
            HashAlgorithmType.MD5, 
            HashAlgorithmType.SHA1, 
            HashAlgorithmType.SHA256, 
            HashAlgorithmType.SHA512 
        };
        
        var hashInfo = new FileHashInfo
        {
            FileSizeBytes = file.Length,
            ComputedAt = DateTime.Now
        };
        
        await Task.Run(() =>
        {
            var totalAlgorithms = algorithms.Length;
            var completedAlgorithms = 0;
            
            foreach (var algorithm in algorithms)
            {
                using var stream = file.OpenRead();
                using var hashAlgorithm = GetHashAlgorithm(algorithm);
                var hashBytes = hashAlgorithm.ComputeHash(stream);
                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                
                switch (algorithm)
                {
                    case HashAlgorithmType.MD5:
                        hashInfo.MD5Hash = hashString;
                        break;
                    case HashAlgorithmType.SHA1:
                        hashInfo.SHA1Hash = hashString;
                        break;
                    case HashAlgorithmType.SHA256:
                        hashInfo.SHA256Hash = hashString;
                        break;
                    case HashAlgorithmType.SHA512:
                        hashInfo.SHA512Hash = hashString;
                        break;
                }
                
                completedAlgorithms++;
                progress?.Report((double)completedAlgorithms / totalAlgorithms);
            }
        });
        
        return hashInfo;
    }
    
    /// <summary>
    /// Verifies if a file matches an expected hash value
    /// </summary>
    /// <param name="file">The file to verify</param>
    /// <param name="expectedHash">The expected hash value</param>
    /// <returns>True if the file matches the expected hash, false otherwise</returns>
    public static async Task<bool> VerifyFileHashAsync(FileInfo? file, string expectedHash)
    {
        if (file == null || !file.Exists || string.IsNullOrWhiteSpace(expectedHash))
            return false;
        
        // Normalize the expected hash
        expectedHash = expectedHash.Replace("-", "").ToLowerInvariant();
        
        // Determine which hash algorithm to use based on the length of the expected hash
        HashAlgorithmType algorithm;
        switch (expectedHash.Length)
        {
            case 32: // MD5 (128 bits = 16 bytes = 32 hex chars)
                algorithm = HashAlgorithmType.MD5;
                break;
            case 40: // SHA-1 (160 bits = 20 bytes = 40 hex chars)
                algorithm = HashAlgorithmType.SHA1;
                break;
            case 64: // SHA-256 (256 bits = 32 bytes = 64 hex chars)
                algorithm = HashAlgorithmType.SHA256;
                break;
            case 128: // SHA-512 (512 bits = 64 bytes = 128 hex chars)
                algorithm = HashAlgorithmType.SHA512;
                break;
            default:
                // Try all algorithms if we can't determine from length
                return await VerifyFileWithAllAlgorithmsAsync(file, expectedHash);
        }
        
        // Compute the hash using the determined algorithm
        var hashInfo = await ComputeMultipleHashesAsync(file, new[] { algorithm });
        var actualHash = algorithm switch
        {
            HashAlgorithmType.MD5 => hashInfo.MD5Hash,
            HashAlgorithmType.SHA1 => hashInfo.SHA1Hash,
            HashAlgorithmType.SHA256 => hashInfo.SHA256Hash,
            HashAlgorithmType.SHA512 => hashInfo.SHA512Hash,
            _ => null
        };
        
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Tries to verify a file against all supported hash algorithms
    /// </summary>
    private static async Task<bool> VerifyFileWithAllAlgorithmsAsync(FileInfo file, string expectedHash)
    {
        var hashInfo = await ComputeMultipleHashesAsync(file);
        
        // Check against all computed hashes
        return string.Equals(hashInfo.MD5Hash, expectedHash, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(hashInfo.SHA1Hash, expectedHash, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(hashInfo.SHA256Hash, expectedHash, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(hashInfo.SHA512Hash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Gets the appropriate hash algorithm instance based on the algorithm type
    /// </summary>
    private static HashAlgorithm GetHashAlgorithm(HashAlgorithmType algorithm)
    {
        return algorithm switch
        {
            HashAlgorithmType.MD5 => MD5.Create(),
            HashAlgorithmType.SHA1 => SHA1.Create(),
            HashAlgorithmType.SHA256 => SHA256.Create(),
            HashAlgorithmType.SHA512 => SHA512.Create(),
            _ => SHA256.Create() // Default to SHA-256
        };
    }
}
