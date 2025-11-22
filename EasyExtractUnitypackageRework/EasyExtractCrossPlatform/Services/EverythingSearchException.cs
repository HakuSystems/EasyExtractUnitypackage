namespace EasyExtractCrossPlatform.Services;

public sealed class EverythingSearchException : Exception
{
    private EverythingSearchException(string message, EverythingErrorCode errorCode, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }

    public EverythingErrorCode ErrorCode { get; }

    public static EverythingSearchException FromError(EverythingErrorCode errorCode)
    {
        return new EverythingSearchException(GetErrorMessage(errorCode), errorCode);
    }

    public static EverythingSearchException MissingLibrary(Exception inner)
    {
        return new EverythingSearchException(
            "Everything64.dll was not found. Place the Everything SDK DLL next to the EasyExtract executable.",
            EverythingErrorCode.LibraryMissing, inner);
    }

    public static EverythingSearchException MismatchedLibrary(Exception inner)
    {
        return new EverythingSearchException(
            "The Everything SDK DLL is incompatible with this application. Verify that Everything64.dll matches your OS architecture.",
            EverythingErrorCode.LibraryMismatch, inner);
    }

    public static EverythingSearchException NativeFailure(Exception inner)
    {
        return new EverythingSearchException("Everything search encountered an unexpected native error.",
            EverythingErrorCode.NativeFailure,
            inner);
    }

    private static string GetErrorMessage(EverythingErrorCode errorCode)
    {
        return errorCode switch
        {
            EverythingErrorCode.Ok => "Search completed successfully.",
            EverythingErrorCode.Memory => "Everything reported an out-of-memory condition while searching.",
            EverythingErrorCode.Ipc =>
                "Unable to reach Everything. Ensure Everything is running in the background and try again.",
            EverythingErrorCode.RegisterClassEx => "Everything failed to register its search window.",
            EverythingErrorCode.CreateWindow => "Everything could not create its search window.",
            EverythingErrorCode.CreateThread => "Everything could not create a helper thread.",
            EverythingErrorCode.InvalidIndex => "Everything reported an invalid result index.",
            EverythingErrorCode.InvalidCall => "Everything reported an invalid call sequence for the query.",
            EverythingErrorCode.LibraryMissing => "Everything SDK library missing.",
            EverythingErrorCode.LibraryMismatch => "Everything SDK library mismatch.",
            EverythingErrorCode.NativeFailure => "Unexpected native failure while querying Everything.",
            _ => $"Everything reported an unknown error ({(int)errorCode})."
        };
    }
}

public enum EverythingErrorCode
{
    Ok = 0,
    Memory = 1,
    Ipc = 2,
    RegisterClassEx = 3,
    CreateWindow = 4,
    CreateThread = 5,
    InvalidIndex = 6,
    InvalidCall = 7,
    LibraryMissing = 1000,
    LibraryMismatch = 1001,
    NativeFailure = 1002
}