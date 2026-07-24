using System.Diagnostics;
using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

/// <summary>
///     The search backends redirect both pipes but only read stdout. A redirected pipe nobody drains
///     fills after a few kilobytes, the child blocks on its next write and WaitForExit never returns.
///     'find /' triggers this in the field by printing a permission denied line per unreadable
///     directory. These tests deliberately produce far more stderr than any pipe buffer holds.
/// </summary>
public sealed class SearchUtilitiesProcessTests
{
    private const int NoiseLines = 4000;

    [Fact]
    public void DrainingStderr_DoesNotDeadlockWhenTheChildFloodsIt()
    {
        using var process = StartNoisyProcess();
        var errorDrain = SearchUtilities.BeginDrainingStandardError(process);

        var stdout = process.StandardOutput.ReadToEnd();

        Assert.True(process.WaitForExit(30_000), "The child process did not exit; the stderr pipe deadlocked.");

        var stderr = SearchUtilities.CollectStandardError(errorDrain);

        Assert.Contains("done", stdout, StringComparison.Ordinal);
        Assert.True(stderr.Length > 64 * 1024,
            $"Expected the child to flood stderr beyond the pipe buffer, saw {stderr.Length} bytes.");
    }

    [Fact]
    public void CollectStandardError_ReturnsEmptyInsteadOfThrowing()
    {
        var faulted = Task.FromException<string>(new InvalidOperationException("pipe is gone"));

        Assert.Equal(string.Empty, SearchUtilities.CollectStandardError(faulted));
    }

    private static Process StartNoisyProcess()
    {
        // Roughly 4000 lines of ~40 characters, comfortably past every platform pipe buffer.
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "cmd.exe";
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(
                $"for /L %i in (1,1,{NoiseLines}) do @echo permission denied: a rather long directory name >&2 & echo done");
        }
        else
        {
            startInfo.FileName = "/bin/sh";
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(
                $"i=0; while [ $i -lt {NoiseLines} ]; do echo 'permission denied: a rather long directory name' >&2; i=$((i+1)); done; echo done");
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start the test process.");
    }
}
