namespace EasyExtract.BetterExtraction;

/// <summary>
/// Represents the type of hash algorithm to use
/// </summary>
public enum HashAlgorithmType
{
    /// <summary>
    /// MD5 hash algorithm (128-bit)
    /// </summary>
    MD5,
    
    /// <summary>
    /// SHA-1 hash algorithm (160-bit)
    /// </summary>
    SHA1,
    
    /// <summary>
    /// SHA-256 hash algorithm (256-bit)
    /// </summary>
    SHA256,
    
    /// <summary>
    /// SHA-512 hash algorithm (512-bit)
    /// </summary>
    SHA512
}
