using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace EasyExtractCrossPlatform.Services;

/// <summary>
///     Keeps a single misbehaving control from taking the whole app down. Avalonia dispatches
///     input through <see cref="Dispatcher" />, so an exception thrown inside a control's key or
///     pointer handler otherwise unwinds straight out of the platform message loop and kills the
///     process. Recoverable failures are logged, reported and swallowed; anything that means the
///     process is already lost is left to <see cref="FatalCrashHandler" />.
/// </summary>
internal static class UiThreadExceptionGuard
{
    private const int WebhookReportLimit = 3;

    private static int _installed;
    private static int _reportedCount;

    public static void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) == 1)
            return;

        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
        LoggingService.LogInformation("UI thread exception guard installed.");
    }

    internal static bool IsRecoverable(Exception? exception)
    {
        if (exception is null)
            return false;

        // The process state is already compromised for these; recovering would only hide the cause.
        if (FatalCrashHandler.IsOutOfMemory(exception))
            return false;

        return !ContainsUnrecoverable(exception, 0);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            if (!IsRecoverable(e.Exception))
                return;

            // A control that throws on every keystroke would otherwise flood the report webhook.
            var reported = Interlocked.Increment(ref _reportedCount);
            LoggingService.LogError(
                $"Recovered from an unhandled UI exception (#{reported}). The app stays usable.",
                e.Exception,
                reported <= WebhookReportLimit);

            e.Handled = true;
        }
        catch
        {
            // Never let the guard itself become the crash.
        }
    }

    private static bool ContainsUnrecoverable(Exception? exception, int depth)
    {
        if (exception is null || depth >= 16)
            return false;

        if (exception is AccessViolationException or SEHException)
            return true;

        if (exception is AggregateException aggregate)
            return aggregate.InnerExceptions.Any(inner => ContainsUnrecoverable(inner, depth + 1));

        return ContainsUnrecoverable(exception.InnerException, depth + 1);
    }
}
