using System.Text.RegularExpressions;

namespace EasyExtractCrossPlatform.Services;

internal sealed class LinuxSearchService : IEverythingSearchService
{
    private static readonly string? FdPath = SearchUtilities.TryResolveExecutable("fd");
    private static readonly string? PlocatePath = SearchUtilities.TryResolveExecutable("plocate");
    private static readonly string? LocatePath = SearchUtilities.TryResolveExecutable("locate");
    private static readonly string? FindPath = SearchUtilities.TryResolveExecutable("find");
    private static readonly string[] DefaultSearchRoots = BuildDefaultSearchRoots();

    private readonly SemaphoreSlim _queryGate = new(1, 1);
    private string _availabilityHint = "Linux search has not been initialized.";
    private int _lastExcludedResultCount;
    private LinuxBackend _resolvedBackend = LinuxBackend.None;

    public int LastExcludedResultCount => Volatile.Read(ref _lastExcludedResultCount);

    public string AvailabilityHint => Volatile.Read(ref _availabilityHint);

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        LoggingService.LogInformation("Evaluating Linux search backend availability.");
        _resolvedBackend = DetermineBackend();
        var available = _resolvedBackend != LinuxBackend.None;
        Volatile.Write(ref _availabilityHint, available
            ? $"Using {DescribeBackend(_resolvedBackend)} to search for .unitypackage files."
            : "No supported search backend (fd, plocate/locate, or find) was discovered on PATH. Install one of these utilities or add it to PATH to enable search.");
        LoggingService.LogInformation(
            $"Linux search backend resolved to {_resolvedBackend} (available={available}).");
        return Task.FromResult(available);
    }

    public async Task<IReadOnlyList<EverythingSearchResult>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        LoggingService.LogInformation($"Linux search requested. Query='{query}', maxResults={maxResults}.");

        if (!OperatingSystem.IsLinux())
        {
            LoggingService.LogInformation("Linux search skipped because current platform is not Linux.");
            return Array.Empty<EverythingSearchResult>();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            LoggingService.LogInformation("Linux search skipped because the query was empty.");
            return Array.Empty<EverythingSearchResult>();
        }

        maxResults = Math.Clamp(maxResults, 1, 2000);
        LoggingService.LogInformation($"Linux search normalized maxResults={maxResults}.");

        var backend = _resolvedBackend != LinuxBackend.None ? _resolvedBackend : DetermineBackend();
        if (backend == LinuxBackend.None)
        {
            LoggingService.LogError("Linux search aborted: no supported backend available.");
            throw new InvalidOperationException("No supported Linux search backend is available.");
        }

        LoggingService.LogInformation($"Linux search will use backend {backend}.");

        var stopwatch = Stopwatch.StartNew();

        var entered = false;
        try
        {
            await _queryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;

            var execution = await Task.Run(() =>
                    ExecuteSearchInternal(backend, query, maxResults, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            Volatile.Write(ref _lastExcludedResultCount, execution.FilteredCount);
            stopwatch.Stop();
            LoggingService.LogInformation(
                $"Linux search completed in {stopwatch.Elapsed.TotalMilliseconds:F0} ms. Results={execution.Results.Count}, excluded={execution.FilteredCount}, backend={backend}.");
            return execution.Results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            LoggingService.LogError($"Linux search failed using backend {backend}.", ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            if (entered)
                _queryGate.Release();
        }
    }

    private static (List<EverythingSearchResult> Results, int FilteredCount) ExecuteSearchInternal(
        LinuxBackend backend,
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        return backend switch
        {
            LinuxBackend.Fd => ExecuteFdSearch(query, maxResults, cancellationToken),
            LinuxBackend.Plocate => ExecuteLocateSearch(PlocatePath!, query, maxResults, cancellationToken),
            LinuxBackend.Locate => ExecuteLocateSearch(LocatePath!, query, maxResults, cancellationToken),
            LinuxBackend.Find => ExecuteFindSearch(query, maxResults, cancellationToken),
            _ => (new List<EverythingSearchResult>(), 0)
        };
    }

    private static (List<EverythingSearchResult> Results, int FilteredCount) ExecuteFdSearch(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        if (FdPath is null)
            throw new InvalidOperationException("fd command could not be located on PATH.");

        var pattern = string.IsNullOrWhiteSpace(query) ? ".*" : Regex.Escape(query);
        var arguments = new List<string>
        {
            "--absolute-path",
            "--type",
            "f",
            "--extension",
            "unitypackage",
            "--max-results",
            Math.Clamp(maxResults, 1, 2000).ToString(CultureInfo.InvariantCulture),
            "--ignore-case",
            pattern.Length == 0 ? ".*" : pattern
        };

        foreach (var root in DefaultSearchRoots)
            arguments.Add(root);

        return ExecuteCommand(FdPath, arguments, maxResults, cancellationToken);
    }

    private static (List<EverythingSearchResult> Results, int FilteredCount) ExecuteLocateSearch(
        string commandPath,
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(maxResults, 1, 5000).ToString(CultureInfo.InvariantCulture);
        var pattern = BuildRegexPattern(query);

        var arguments = new List<string>
        {
            "-i",
            "-n",
            limit,
            "-r",
            pattern
        };

        return ExecuteCommand(commandPath, arguments, maxResults, cancellationToken, true);
    }

    private static (List<EverythingSearchResult> Results, int FilteredCount) ExecuteFindSearch(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        if (FindPath is null)
            throw new InvalidOperationException("find command could not be located on PATH.");

        var arguments = new List<string>();
        arguments.AddRange(DefaultSearchRoots);
        arguments.Add("-type");
        arguments.Add("f");
        arguments.Add("-iregex");
        arguments.Add(BuildRegexPattern(query));

        return ExecuteCommand(FindPath, arguments, maxResults, cancellationToken);
    }

    private static (List<EverythingSearchResult> Results, int FilteredCount) ExecuteCommand(
        string commandPath,
        IReadOnlyList<string> arguments,
        int maxResults,
        CancellationToken cancellationToken,
        bool treatNoMatchesAsSuccess = false)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = commandPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            processStartInfo.ArgumentList.Add(argument);

        using var process = Process.Start(processStartInfo) ??
                            throw new InvalidOperationException($"Unable to start process '{commandPath}'.");

        var commandSummary = $"{commandPath} {string.Join(' ', arguments)}".Trim();
        LoggingService.LogInformation($"Executing Linux search backend command: {commandSummary}");

        using var cancellationRegistration = cancellationToken.Register(() => { TryTerminate(process); });

        var (results, excluded) = CollectResults(process, maxResults, cancellationToken);

        process.WaitForExit();
        var errorOutput = process.StandardError.ReadToEnd();

        var noMatchExit = treatNoMatchesAsSuccess &&
                          process.ExitCode == 1 &&
                          string.IsNullOrWhiteSpace(errorOutput);

        if (process.ExitCode != 0 &&
            !cancellationToken.IsCancellationRequested &&
            !noMatchExit)
        {
            var message =
                $"{Path.GetFileName(commandPath)} exited with code {process.ExitCode}: {errorOutput}";
            LoggingService.LogError($"Linux search backend command failed. {message}");
            throw new InvalidOperationException(message);
        }

        LoggingService.LogInformation(
            $"Linux search backend command completed with exit code {process.ExitCode}. Results={results.Count}, excluded={excluded}.");

        return (results, excluded);
    }

    private static (List<EverythingSearchResult> Results, int FilteredCount) CollectResults(
        Process process,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<EverythingSearchResult>();
        var seenComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var seen = new HashSet<string>(seenComparer);
        var excluded = 0;

        while (!process.StandardOutput.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawLine = process.StandardOutput.ReadLine();
            if (rawLine is null)
                break;

            var candidate = rawLine.Trim();
            if (candidate.Length == 0)
                continue;

            if (!candidate.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!seen.Add(candidate))
                continue;

            var result = SearchUtilities.BuildResultFromPath(candidate);
            if (result is null)
            {
                excluded++;
                continue;
            }

            results.Add(result);
            if (results.Count >= maxResults)
            {
                TryTerminate(process);
                break;
            }
        }

        return (results, excluded);
    }

    private static string BuildRegexPattern(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return @".*\.unitypackage$";

        return @$".*{Regex.Escape(query)}.*\.unitypackage$";
    }

    private static LinuxBackend DetermineBackend()
    {
        if (!string.IsNullOrEmpty(FdPath))
            return LinuxBackend.Fd;
        if (!string.IsNullOrEmpty(PlocatePath))
            return LinuxBackend.Plocate;
        if (!string.IsNullOrEmpty(LocatePath))
            return LinuxBackend.Locate;
        if (!string.IsNullOrEmpty(FindPath))
            return LinuxBackend.Find;
        return LinuxBackend.None;
    }

    private static string DescribeBackend(LinuxBackend backend)
    {
        return backend switch
        {
            LinuxBackend.Fd => "fd (https://github.com/sharkdp/fd)",
            LinuxBackend.Plocate => "plocate",
            LinuxBackend.Locate => "locate",
            LinuxBackend.Find => "find",
            _ => "unknown backend"
        };
    }

    private static string[] BuildDefaultSearchRoots()
    {
        var roots = new List<string>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home) && Directory.Exists(home))
            roots.Add(home);

        foreach (var candidate in new[] { "/mnt", "/media" })
            if (Directory.Exists(candidate))
                roots.Add(candidate);

        if (roots.Count == 0)
            roots.Add("/");

        return roots.ToArray();
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch
        {
            // Process already exited or could not be terminated. Ignore.
        }
    }

    private enum LinuxBackend
    {
        None,
        Fd,
        Plocate,
        Locate,
        Find
    }
}