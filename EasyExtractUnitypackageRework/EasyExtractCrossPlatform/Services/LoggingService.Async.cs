using System.Text;

namespace EasyExtractCrossPlatform.Services;

public static partial class LoggingService
{
    private static bool TryQueueEntry(PendingLogEntry entry)
    {
        if (!entry.Preferences.AsyncLoggingEnabled)
            return false;

        EnsureAsyncProcessingLoop();

        PendingEntries.Enqueue(entry);
        try
        {
            LogSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            if (PendingEntries.TryDequeue(out var overflowEntry))
                WriteEntryCore(overflowEntry);
        }

        return true;
    }

    private static void WriteEntryCore(PendingLogEntry entry)
    {
        var builder = new StringBuilder();
        builder.Append('[')
            .Append(entry.Level)
            .Append("] ")
            .Append(" V" + VersionProvider.GetApplicationVersion() + " ")
            .Append(entry.Message);

        if (entry.Exception is not null)
        {
            builder.AppendLine();
            if (entry.Preferences.CaptureStackTraces)
                builder.Append(entry.Exception);
            else
                builder.Append(entry.Exception.GetType().FullName)
                    .Append(':')
                    .Append(' ')
                    .Append(entry.Exception.Message);
        }
        else if (!string.IsNullOrWhiteSpace(entry.StackTrace))
        {
            builder.AppendLine();
            builder.Append(entry.StackTrace);
        }

        var formattedPayload = builder.ToString();
        DispatchLogPayload(formattedPayload);

        if (string.Equals(entry.Level, "ERROR", StringComparison.OrdinalIgnoreCase))
        {
            if (entry.ForwardToWebhook)
                QueueHakuWebhookDispatch(formattedPayload);
            NotifyErrorObservers(entry, formattedPayload);
        }
    }

    private static void DispatchLogPayload(string payload)
    {
        var wroteToCustomListener = false;

        if (_listener is { } listener)
            try
            {
                listener.WriteLine(payload);
                wroteToCustomListener = true;
            }
            catch (ObjectDisposedException)
            {
                // Listener was disposed between writes; fall through to Trace/Debugger.
            }

        if (!wroteToCustomListener)
            Trace.WriteLine(payload);

        if (Debugger.IsAttached)
            Debugger.Log(0, "EasyExtract", payload + Environment.NewLine);
    }

    private static void EnsureAsyncProcessingLoop()
    {
        if (_logProcessingTask is { IsCompleted: false })
            return;

        lock (SyncRoot)
        {
            if (_logProcessingTask is { IsCompleted: false })
                return;

            _logProcessingCts?.Dispose();
            _logProcessingCts = new CancellationTokenSource();
            _logProcessingTask = Task.Run(() => ProcessLogQueueAsync(_logProcessingCts.Token));
        }
    }

    private static void StopAsyncProcessingLoop(bool flushQueue)
    {
        CancellationTokenSource? cts;
        Task? worker;

        lock (SyncRoot)
        {
            if (_logProcessingTask is null)
            {
                if (flushQueue)
                    FlushPendingEntries();
                return;
            }

            cts = _logProcessingCts;
            worker = _logProcessingTask;
            _logProcessingTask = null;
            _logProcessingCts = null;
        }

        try
        {
            cts?.Cancel();
            LogSignal.Release();
            if (worker is not null)
                try
                {
                    worker.WaitAsync(TimeSpan.FromSeconds(2))
                        .GetAwaiter()
                        .GetResult();
                }
                catch (TimeoutException)
                {
                    WriteEntry("WARN", "Logging background worker did not shut down within timeout.",
                        null, bypassAsyncPipeline: true);
                }
                catch (OperationCanceledException)
                {
                    // Expected if worker observes cancellation as fault.
                }
                catch (ObjectDisposedException)
                {
                    // Worker already cleaned up.
                }
                catch (Exception ex)
                {
                    WriteEntry("WARN", "Logging background worker faulted during shutdown.",
                        ex, bypassAsyncPipeline: true);
                }
        }
        catch (ObjectDisposedException)
        {
            // Semaphore already disposed during shutdown.
        }
        finally
        {
            cts?.Dispose();
        }

        if (flushQueue)
            FlushPendingEntries();
    }

    private static async Task ProcessLogQueueAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await LogSignal.WaitAsync(token).ConfigureAwait(false);
                FlushPendingEntries();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            WriteEntry("ERROR", "Logging background pipeline faulted.", ex, bypassAsyncPipeline: true);
        }
        finally
        {
            FlushPendingEntries();
        }
    }

    private static void FlushPendingEntries()
    {
        while (PendingEntries.TryDequeue(out var entry))
            WriteEntryCore(entry);
    }

    private static void QueueHakuWebhookDispatch(string payload)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var client = HakuWebhookClientProvider.Instance;
                var version = VersionProvider.GetApplicationVersion();
                await client.SendErrorReportAsync(payload, version).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Webhook] Failed to forward log payload: {ex.Message}");
            }
        });
    }
}