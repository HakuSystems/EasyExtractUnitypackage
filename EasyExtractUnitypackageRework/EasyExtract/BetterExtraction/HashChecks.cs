using System.Security.Cryptography;
using System.Text;

namespace EasyExtract.BetterExtraction;

public class HashChecks
{
    public string ComputeFileHash(FileInfo file)
    {
        using var stream = file.OpenRead();
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(stream);
        var sb = new StringBuilder();
        foreach (var b in hashBytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}