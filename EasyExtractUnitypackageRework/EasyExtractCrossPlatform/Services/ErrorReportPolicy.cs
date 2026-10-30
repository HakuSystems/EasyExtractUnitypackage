using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;

namespace EasyExtractCrossPlatform.Services;

/// <summary>
///     Decides whether an error is worth forwarding to the remote error-report webhook.
///     Expected, environment-caused failures (cancellations, timeouts, network problems)
///     stay in the local log only so the report channel keeps its signal.
/// </summary>
public static class ErrorReportPolicy
{
    private const int MaxInspectionDepth = 10;

    private const int ErrorNotReady = unchecked((int)0x80070015);
    private const int ErrorCrc = unchecked((int)0x80070017);
    private const int ErrorDevNotExist = unchecked((int)0x80070037);
    private const int ErrorNoSuchDevice = unchecked((int)0x800701B1);
    private const int ErrorFileInvalid = unchecked((int)0x800703EE);
    private const int ErrorIoDevice = unchecked((int)0x8007045D);
    private const int ErrorDeviceNotConnected = unchecked((int)0x8007048F);

    public static bool ShouldForward(Exception? exception)
    {
        return !ContainsOnlyExpectedFailure(exception, 0);
    }

    private static bool ContainsOnlyExpectedFailure(Exception? exception, int depth)
    {
        if (exception is null || depth >= MaxInspectionDepth)
            return false;

        if (exception is AggregateException aggregate)
        {
            var inner = aggregate.Flatten().InnerExceptions;
            return inner.Count > 0 && inner.All(child => ContainsOnlyExpectedFailure(child, depth + 1));
        }

        if (IsExpected(exception))
            return true;

        return ContainsOnlyExpectedFailure(exception.InnerException, depth + 1);
    }

    private static bool IsExpected(Exception exception)
    {
        return exception is OperationCanceledException
                   or TimeoutException
                   or HttpRequestException
                   or SocketException
                   or WebException
                   or AuthenticationException
               || IsStorageDeviceFailure(exception)
               || FatalCrashHandler.IsMissingDisplay(exception);
    }

    // The drive was unplugged, went to sleep or is failing. Nothing the app can fix, and the
    // user already sees the system message on the extraction that hit it.
    private static bool IsStorageDeviceFailure(Exception exception)
    {
        return exception is IOException
        {
            HResult: ErrorNotReady or ErrorCrc or ErrorDevNotExist or ErrorNoSuchDevice or ErrorFileInvalid
            or ErrorIoDevice or ErrorDeviceNotConnected
        };
    }
}
