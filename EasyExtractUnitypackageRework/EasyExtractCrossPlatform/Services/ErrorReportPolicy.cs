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
            or AuthenticationException;
    }
}
