using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace EasyExtract.Utilities.Logger;
//Documented by AI

/// <summary>
///     A static logger utility class providing robust functionalities for logging messages,
///     enabling stack traces, tracking system performance, and managing application diagnostics.
/// </summary>
public static class BetterLogger
{
    /// <summary>
    ///     An object used to synchronize access to critical sections in the logger.
    /// </summary>
    /// <remarks>
    ///     This field ensures thread-safe operations when accessing shared resources
    ///     such as the log queue or performing write operations in a multi-threaded environment.
    /// </remarks>
    private static readonly object _lockObject = new();

    /// <summary>
    ///     Represents the primary file-based log stream used for logging general information.
    ///     This log stream is initialized and managed internally by the <c>BetterLogger</c> class
    ///     and is written to the "Log_Main.txt" file located in the application's log directory.
    /// </summary>
    /// <remarks>
    ///     The file log is used for recording standard log messages across various categories and log levels
    ///     unless they are specifically categorized for other dedicated logs (e.g., performance or error logs).
    ///     It is initialized during the <c>BetterLogger.InitializeLoggers</c> method, where old log files are
    ///     cleaned and the log file is prepared for writing. The log entries are formatted and written in
    ///     the <c>BetterLogger.WriteLogEntry</c> method, and the log data is periodically flushed using the
    ///     <c>BetterLogger.FlushLogs</c> method to ensure data integrity.
    ///     The stream is closed and resources are released during the <c>BetterLogger.Shutdown</c> process.
    /// </remarks>
    /// <seealso cref="BetterLogger.InitializeLoggers" />
    /// <seealso cref="BetterLogger.WriteLogEntry" />
    /// <seealso cref="BetterLogger.FlushLogs" />
    /// <seealso cref="BetterLogger.Shutdown" />
    private static StreamWriter? _fileLog;

    /// <summary>
    ///     A private static variable used to write and manage performance log entries.
    /// </summary>
    /// <remarks>
    ///     This variable is initialized when loggers are set up in the application, pointing to a log file
    ///     named "Log_Performance.txt". It specifically logs performance-related information, and entries
    ///     are written only when performance logging is enabled. The log is auto-flushed after each write to ensure
    ///     data consistency.
    /// </remarks>
    private static StreamWriter? _performanceLog;

    /// <summary>
    ///     Represents a StreamWriter instance dedicated to logging error messages.
    /// </summary>
    /// <remarks>
    ///     The error log is initialized during the setup process in <c>BetterLogger.InitializeLoggers()</c>.
    ///     It is used specifically for writing log entries that correspond to error-level or higher log messages.
    ///     The log data is stored in a file named "Log_Errors.txt" within the designated logging directory.
    /// </remarks>
    /// <seealso cref="BetterLogger.InitializeLoggers" />
    /// <seealso cref="BetterLogger.WriteLogEntry" />
    /// <seealso cref="BetterLogger.FlushLogs" />
    /// <seealso cref="BetterLogger.Shutdown" />
    private static StreamWriter? _errorLog;

    /// <summary>
    ///     Represents the private static StreamWriter instance used for logging debug information within the BetterLogger
    ///     class.
    /// </summary>
    /// <remarks>
    ///     This StreamWriter instance is specifically responsible for writing debug-level log entries to a separate file
    ///     (e.g., "Log_Debug.txt"). It is initialized during logger setup and is closed and disposed of properly during
    ///     shutdown to ensure no resource leaks occur. Debug logs generally include detailed information useful for
    ///     debugging and tracing application flow.
    /// </remarks>
    private static StreamWriter? _debugLog;

    /// <summary>
    ///     A static Stopwatch instance used to measure elapsed time for logging
    ///     and performance tracking purposes in the BetterLogger class.
    /// </summary>
    /// <remarks>
    ///     This Stopwatch is initialized and started when the BetterLogger class is loaded.
    ///     It is primarily utilized to calculate elapsed time in milliseconds for log entries
    ///     and to track application uptime during performance metrics computation.
    /// </remarks>
    private static readonly Stopwatch _globalStopwatch = Stopwatch.StartNew();

    /// <summary>
    ///     Represents a thread-safe queue used to store log entries before they are processed.
    ///     Allows for asynchronous logging by enqueuing log entries and processing them
    ///     in a separate operation or thread, improving logging performance and reducing latency.
    /// </summary>
    private static readonly ConcurrentQueue<LogEntry> _logQueue = new();

    /// <summary>
    ///     A timer used to periodically trigger the flushing of logs.
    /// </summary>
    /// <remarks>
    ///     This timer is initialized to execute the log flushing operation at regular intervals,
    ///     ensuring that pending log messages are written to the output destination in a timely manner.
    ///     It is disposed during the shutdown process to release resources.
    /// </remarks>
    private static readonly Timer? _flushTimer;

    /// <summary>
    ///     A timer used to periodically log performance metrics. Initialized during the
    ///     construction of the <see cref="BetterLogger" /> class and configured to trigger
    ///     performance logging operations at a specified interval.
    /// </summary>
    /// <remarks>
    ///     This timer is utilized to gather and log system performance data, such as memory
    ///     usage and execution time, to assist in monitoring and debugging the application performance.
    ///     It is internally managed by the logger and disposed of during the application shutdown
    ///     process.
    /// </remarks>
    private static readonly Timer? _metricsTimer;

    /// <summary>
    ///     A dictionary used for tracking performance metrics by associating unique string keys
    ///     with <see cref="System.Diagnostics.Stopwatch" /> instances. It helps measure and log
    ///     the time taken by various operations when performance logging is enabled.
    /// </summary>
    private static readonly Dictionary<string, Stopwatch> _performanceCounters = new();

    /// <summary>
    ///     A private static dictionary that tracks event counters by their names and associated values.
    /// </summary>
    /// <remarks>
    ///     This dictionary is primarily used within the logging system to maintain and track the counts
    ///     of specific events or occurrences. It helps monitor and log event-related metrics.
    /// </remarks>
    private static readonly Dictionary<string, long> _eventCounters = new();

    /// <summary>
    ///     Represents the minimum logging level for the logger.
    ///     Only log messages at or above this level will be processed and recorded.
    /// </summary>
    private static LogLevel _minLogLevel = LogLevel.Trace;

    /// <summary>
    ///     Indicates whether stack traces should be generated and included in log entries.
    ///     When enabled, stack traces will be added to log entries with a log level of Warning or higher.
    /// </summary>
    private static bool _enableStackTrace = true;

    /// <summary>
    ///     Indicates whether performance logging is enabled within the application.
    /// </summary>
    /// <remarks>
    ///     When set to true, performance-related metrics such as the start and stop times of specific operations
    ///     are captured and logged. This can help in diagnosing performance issues and measuring execution times
    ///     of different parts of the system.
    /// </remarks>
    private static bool _enablePerformanceLogging = true;

    /// <summary>
    ///     Determines whether memory usage tracking is enabled during log creation.
    /// </summary>
    /// <remarks>
    ///     When set to <c>true</c>, log entries will include memory usage information (in bytes) retrieved
    ///     from the garbage collector. This may be useful for identifying memory consumption trends
    ///     during the application's operation. Excessive memory tracking may have a slight performance impact.
    /// </remarks>
    private static bool _enableMemoryTracking = true;

    /// <summary>
    ///     Indicates whether asynchronous logging is enabled.
    /// </summary>
    /// <remarks>
    ///     When set to true, log entries are queued and processed asynchronously,
    ///     allowing non-blocking logging operations. If set to false, log entries are
    ///     written immediately in a synchronous manner. This can impact application
    ///     performance depending on the chosen mode.
    /// </remarks>
    private static bool _enableAsyncLogging = true;

    /// <summary>
    ///     A <see cref="StringBuilder" /> instance used for constructing formatted log messages
    ///     efficiently within the logger. Initialized with a default capacity of 1024.
    /// </summary>
    /// <remarks>
    ///     This internal field is used by the logger to minimize allocations and improve performance
    ///     when generating log messages. It is cleared before each usage to ensure no data leakage
    ///     occurs between log entries.
    /// </remarks>
    private static readonly StringBuilder _stringBuilder = new(1024);

    /// Provides enhanced logging utility with features such as configurable log levels, stack tracing,
    /// performance tracking, memory usage monitoring, and asynchronous logging.
    /// This class is designed to help developers capture detailed runtime information efficiently.
    static BetterLogger()
    {
        InitializeLoggers();
        _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        _metricsTimer = new Timer(LogPerformanceMetrics, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        Info("BetterLogger logging system initialized", "Initialization");
    }

    /// <summary>
    ///     Initializes the logging system by creating necessary log files and directories,
    ///     preparing the required log streams, and cleaning up old log files.
    /// </summary>
    /// <remarks>
    ///     This method sets up the primary logging infrastructure. It ensures that log files
    ///     are stored in a dedicated directory within the user's application data folder.
    ///     Any outdated log files are removed during this process. Additionally, headers are
    ///     added to the log files to indicate their purpose (e.g., main log, performance log).
    ///     If the initialization process fails due to any exception, details of the failure
    ///     will be output to the console.
    /// </remarks>
    private static void InitializeLoggers()
    {
        try
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasyExtract", "Logs");
            if (!Directory.Exists(logPath))
                Directory.CreateDirectory(logPath);

            // Clean up old log files before creating new ones
            CleanupOldLogFiles(logPath);

            _fileLog = new StreamWriter(Path.Combine(logPath, "Log_Main.txt"), false, Encoding.UTF8)
                { AutoFlush = false };
            _performanceLog = new StreamWriter(Path.Combine(logPath, "Log_Performance.txt"), false, Encoding.UTF8)
                { AutoFlush = false };
            _errorLog = new StreamWriter(Path.Combine(logPath, "Log_Errors.txt"), false, Encoding.UTF8)
                { AutoFlush = false };
            _debugLog = new StreamWriter(Path.Combine(logPath, "Log_Debug.txt"), false, Encoding.UTF8)
                { AutoFlush = false };

            WriteLogHeader(_fileLog, "MAIN LOG");
            WriteLogHeader(_performanceLog, "PERFORMANCE LOG");
            WriteLogHeader(_errorLog, "ERROR LOG");
            WriteLogHeader(_debugLog, "DEBUG LOG");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize BetterLogger: {ex}");
        }
    }

    /// Cleans up old log files in the specified directory by deleting files that match the log file pattern.
    /// <param name="logPath">The path of the directory containing log files to be cleaned up.</param>
    private static void CleanupOldLogFiles(string logPath)
    {
        try
        {
            var directory = new DirectoryInfo(logPath);
            if (!directory.Exists) return;

            var files = directory.GetFiles("Log_*.txt");
            foreach (var file in files)
                try
                {
                    file.Delete();
                }
                catch (IOException)
                {
                    // File is likely in use by another process, skip it.
                }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during log file cleanup: {ex.Message}");
        }
    }

    /// Writes a header section to a log file, providing metadata about the application and environment.
    /// <param name="writer">
    ///     The StreamWriter instance to write the log header to. If null, no action is taken.
    /// </param>
    /// <param name="logType">
    ///     The type or name of the log (e.g., "MAIN LOG", "ERROR LOG") written as part of the header.
    /// </param>
    private static void WriteLogHeader(StreamWriter? writer, string logType)
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var version = entryAssembly?.GetName().Version?.ToString() ?? "N/A";

        writer?.WriteLine($"=== {logType} - EasyExtract v{version} ===");
        writer?.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer?.WriteLine(
            $"Process: {Process.GetCurrentProcess().ProcessName} (PID: {Process.GetCurrentProcess().Id})");
        writer?.WriteLine($"OS: {Environment.OSVersion}");
        writer?.WriteLine($"Runtime: {Environment.Version}");
        writer?.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        writer?.WriteLine($"Working Directory: {Environment.CurrentDirectory}");
        writer?.WriteLine($"Machine: {Environment.MachineName}");
        writer?.WriteLine($"User: {Environment.UserName}");
        writer?.WriteLine($"Processors: {Environment.ProcessorCount}");
        writer?.WriteLine(new string('=', 80));
        writer?.WriteLine();
    }

    /// Sets the minimum logging level for the logger. Logs below the specified level will be filtered out.
    /// <param name="level">The minimum log level to be set. Valid values are Trace, Debug, Info, Warning, Error, and Critical.</param>
    public static void SetLogLevel(LogLevel level)
    {
        _minLogLevel = level;
    }

    /// Enables or disables logging of stack trace information in the logs.
    /// <param name="enable">
    ///     Specifies whether stack trace logging should be enabled.
    ///     Pass true to enable stack trace logging; otherwise, false to disable it.
    /// </param>
    public static void EnableStackTrace(bool enable)
    {
        _enableStackTrace = enable;
    }

    /// Enables or disables performance logging.
    /// <param name="enable">
    ///     A boolean indicating whether to enable or disable performance logging. Pass true to enable
    ///     performance logging and false to disable it.
    /// </param>
    public static void EnablePerformanceLogging(bool enable)
    {
        _enablePerformanceLogging = enable;
    }

    /// Enables or disables memory tracking in the logger.
    /// <param name="enable">A boolean value indicating whether to enable or disable memory tracking.</param>
    public static void EnableMemoryTracking(bool enable)
    {
        _enableMemoryTracking = enable;
    }

    /// Enables or disables asynchronous logging within the logging system.
    /// Asynchronous logging allows log messages to be processed in a background thread,
    /// which can improve application performance by preventing log processing from blocking
    /// the main application flow.
    /// This setting can be useful for high-throughput applications where logging
    /// operations might otherwise become a bottleneck, especially when writing to
    /// slower mediums such as files or remote systems.
    /// Parameters:
    /// enable:
    /// A boolean value indicating whether asynchronous logging should be enabled (`true`)
    /// or disabled (`false`).
    public static void EnableAsyncLogging(bool enable)
    {
        _enableAsyncLogging = enable;
    }

    #region Main Logging Methods

    /// Provides logging functionality with various levels of severity, categories, and additional context information.
    /// This method is part of the `BetterLogger` class and can be used to log messages with optional metadata.
    /// Parameters:
    /// message: The message or object to be logged.
    /// level: The log level specifying the severity of the log entry. Defaults to `LogLevel.Info`.
    /// category: The category to associate with the log entry. Defaults to "General".
    /// callerMethod: The name of the method from which the log call originated. Automatically provided using the `CallerMemberName` attribute.
    /// callerFile: The file path of the source file that contains the log call. Automatically provided using the `CallerFilePath` attribute.
    /// callerLine: The line number where the log call occurs in the source code. Automatically provided using the `CallerLineNumber` attribute.
    /// Remarks:
    /// - The method checks the log level against the minimum log level set in the logger.
    /// - If the specified log level is less severe than the minimum log level, the log entry is ignored.
    /// See Also:
    /// - `Trace`, `Debug`, `Info`, `Warning`, `Error`, and `Critical` methods for convenience methods based on the log level.
    /// - `SetLogLevel` for setting the minimum log level for logging.
    /// Example Usage:
    /// This is internally invoked by other log level-specific methods and is not designed for direct use in most scenarios.
    private static void Log(object message, LogLevel level = LogLevel.Info, string category = "General",
        [CallerMemberName] string callerMethod = "", [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        if (level < _minLogLevel) return;
        var entry = CreateLogEntry(message, level, category, callerMethod, callerFile, callerLine);
        ProcessLogEntry(entry);
    }

    /// <summary>
    ///     Logs a message with a log level of <see cref="LogLevel.Trace" />.
    /// </summary>
    /// <param name="message">The message to be logged.</param>
    /// <param name="category">The category associated with the log entry. Defaults to "Trace".</param>
    /// <param name="callerMethod">The name of the caller method. Automatically provided by the runtime.</param>
    /// <param name="callerFile">The file path of the caller. Automatically provided by the runtime.</param>
    /// <param name="callerLine">
    ///     The line number in the file where the logging method is called. Automatically provided by the
    ///     runtime.
    /// </param>
    public static void Trace(object message, string category = "Trace",
        [CallerMemberName] string callerMethod = "", [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        Log(message, LogLevel.Trace, category, callerMethod, callerFile, callerLine);
    }

    /// Logs a debug-level message to the logging system.
    /// <param name="message">The message or object to be logged.</param>
    /// <param name="category">The category under which this message is logged. Defaults to "Debug".</param>
    /// <param name="callerMethod">The name of the method that called this function. Automatically populated by the compiler.</param>
    /// <param name="callerFile">The source file containing the calling method. Automatically populated by the compiler.</param>
    /// <param name="callerLine">
    ///     The line number in the source file where this function was called.Automatically populated by
    ///     the compiler.
    /// </param>
    public static void Debug(object message, string category = "Debug",
        [CallerMemberName] string callerMethod = "", [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        Log(message, LogLevel.Debug, category, callerMethod, callerFile, callerLine);
    }

    /// Logs an informational message with the specified category and caller metadata.
    /// This is used for standard informational logging, often to indicate the current state or flow of an application.
    /// <param name="message">
    ///     The message to log. Can be any object. If not already a string, its string representation will be
    ///     logged.
    /// </param>
    /// <param name="category">
    ///     The category of the log message, representing the context or logical grouping for the log.
    ///     Defaults to "Info".
    /// </param>
    /// <param name="callerMethod">
    ///     The name of the method where this call was made. This is automatically captured by the
    ///     compiler.
    /// </param>
    /// <param name="callerFile">
    ///     The full file path of the source code where this call was made. This is automatically captured
    ///     by the compiler.
    /// </param>
    /// <param name="callerLine">
    ///     The line number in the source code where this call was made.This is automatically captured by
    ///     the compiler.
    /// </param>
    public static void Info(object message, string category = "Info",
        [CallerMemberName] string callerMethod = "", [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        Log(message, LogLevel.Info, category, callerMethod, callerFile, callerLine);
    }

    /// Logs a warning-level message to the logging system.
    /// <param name="message">
    ///     The message to log. This can be any object, and its string representation
    ///     will be used for logging.
    /// </param>
    /// <param name="category">
    ///     The category under which the log should fall. Default value is "Warning".
    /// </param>
    /// <param name="callerMethod">
    ///     The method from which this log call originated. This is automatically
    ///     populated using the caller information attribute.
    /// </param>
    /// <param name="callerFile">
    ///     The file path of the source file that contains the calling method. This is
    ///     automatically populated using the caller information attribute.
    /// </param>
    /// <param name="callerLine">
    ///     The line number in the source file where the log call is made. This is
    ///     automatically populated using the caller information attribute.
    /// </param>
    public static void Warning(object message, string category = "Warning",
        [CallerMemberName] string callerMethod = "", [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        Log(message, LogLevel.Warning, category, callerMethod, callerFile, callerLine);
    }

    /// Logs an error message to the logging system. The message will only be logged if the current
    /// logging level is set to LogLevel.Error or lower. Additional information about the calling
    /// method, file, and line number is also captured if provided.
    /// <param name="message">The error message to be logged. This can be any object that can be converted to a string.</param>
    /// <param name="category">An optional category to identify the type or context of the log message. Defaults to "Error".</param>
    /// <param name="callerMethod">
    ///     The name of the calling method automatically provided by the CallerMemberName attribute.
    ///     This is passed automatically and does not need to be provided explicitly.
    /// </param>
    /// <param name="callerFile">
    ///     The file path of the calling method automatically provided by the CallerFilePath attribute.
    ///     This is passed automatically and does not need to be provided explicitly.
    /// </param>
    /// <param name="callerLine">
    ///     The line number in the file of the calling method automatically provided by the CallerLineNumber
    ///     attribute. This is passed automatically and does not need to be provided explicitly.
    /// </param>
    public static void Error(object message, string category = "Error",
        [CallerMemberName] string callerMethod = "", [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        Log(message, LogLevel.Error, category, callerMethod, callerFile, callerLine);
    }

    /// Logs a message with the `Critical` log level. This indicates a severe issue that requires immediate attention.
    /// <param name="message">The message or object to be logged at the `Critical` level.</param>
    /// <param name="category">The category of the log entry. Defaults to "Critical".</param>
    /// <param name="callerMethod">The name of the method that invoked the log. Automatically set by the caller's context.</param>
    /// <param name="callerFile">
    ///     The file path of the source code where the log method was called. Automatically set by the
    ///     caller's context.
    /// </param>
    /// <param name="callerLine">
    ///     The line number in the source code where the log method was called. Automatically set by the
    ///     caller's context.
    /// </param>
    public static void Critical(object message, string category = "Critical",
        [CallerMemberName] string callerMethod = "", [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        Log(message, LogLevel.Critical, category, callerMethod, callerFile, callerLine);
    }

    /// Logs an exception message with additional context information for debugging purposes.
    /// <param name="ex">The exception to be logged.</param>
    /// <param name="message">An optional custom message to include with the logged exception. Defaults to an empty string.</param>
    /// <param name="category">The category of the log entry. Defaults to "Exception".</param>
    /// <param name="callerMethod">
    ///     The name of the method that called this method. This is provided automatically by the
    ///     compiler.
    /// </param>
    /// <param name="callerFile">
    ///     The file path of the source code that called this method. This is provided automatically by
    ///     the compiler.
    /// </param>
    /// <param name="callerLine">
    ///     The line number in the source code that called this method. This is provided automatically by
    ///     the compiler.
    /// </param>
    public static void Exception(Exception ex, string message = "", string category = "Exception",
        [CallerMemberName] string callerMethod = "", [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        var fullMessage = string.IsNullOrEmpty(message) ? ex.Message : $"{message}: {ex.Message}";
        var entry = CreateLogEntry(fullMessage, LogLevel.Error, category, callerMethod, callerFile, callerLine);
        entry.Exception = ex;
        ProcessLogEntry(entry);
    }

    #endregion

    #region Context and Performance Logging

    /// Logs a message along with additional contextual information.
    /// The method supports adding key-value context pairs to a log entry, and logs the message
    /// along with the specified log level, category, and source information including the
    /// calling method, file, and line number.
    /// <param name="message">The main content of the log entry.</param>
    /// <param name="context">A dictionary containing additional contextual information to include in the log entry.</param>
    /// <param name="level">The log level for the message. Defaults to LogLevel.Info.</param>
    /// <param name="category">The category of the log message. Defaults to "Context".</param>
    /// <param name="callerMethod">The name of the calling method. This parameter is automatically populated by the compiler.</param>
    /// <param name="callerFile">The file path of the calling code. This parameter is automatically populated by the compiler.</param>
    /// <param name="callerLine">
    ///     The line number of the calling code. This parameter is automatically populated by the
    ///     compiler.
    /// </param>
    public static void LogWithContext(object message, Dictionary<string, object> context,
        LogLevel level = LogLevel.Info, string category = "Context",
        [CallerMemberName] string callerMethod = "", [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0)
    {
        var entry = CreateLogEntry(message, level, category, callerMethod, callerFile, callerLine);
        foreach (var kvp in context)
            entry.Context[kvp.Key] = kvp.Value;
        ProcessLogEntry(entry);
    }

    /// Begins a named logging scope to track performance or categorize log entries.
    /// Automatically logs entering and exiting the scope, including duration if applicable.
    /// <param name="scopeName">The name of the scope to identify it in the logs.</param>
    /// <param name="category">The logging category associated with this scope (default is "Performance").</param>
    /// <return>An IDisposable instance that represents the logging scope. Dispose of it to end the scope.</return>
    public static IDisposable BeginScope(string scopeName, string category = "Performance")
    {
        return new LogScope(scopeName, category);
    }

    /// Starts a performance counter with the specified name. If performance logging is disabled, the method does nothing.
    /// <param name="name">
    ///     The name of the performance counter to start. This should be a unique identifier to track the
    ///     specific operation's performance.
    /// </param>
    public static void StartPerformanceCounter(string name)
    {
        if (!_enablePerformanceLogging) return;
        _performanceCounters[name] = Stopwatch.StartNew();
    }

    /// Stops a performance counter with the specified name and logs its duration if performance logging is enabled.
    /// Removes the performance counter from the tracking dictionary after logging.
    /// <param name="name">The name of the performance counter to stop and log.</param>
    /// <param name="category">
    ///     The logging category to associate with the performance data. Default is "Performance".
    /// </param>
    public static void StopPerformanceCounter(string name, string category = "Performance")
    {
        if (!_enablePerformanceLogging || !_performanceCounters.TryGetValue(name, out var sw)) return;

        sw.Stop();
        LogWithContext($"Performance: {name}", new Dictionary<string, object>
        {
            ["Duration"] = $"{sw.ElapsedMilliseconds}ms",
            ["Ticks"] = sw.ElapsedTicks
        }, LogLevel.Debug, category);

        _performanceCounters.Remove(name);
    }

    /// Increments the value of a specified counter by a given amount.
    /// If the counter does not exist, it is initialized with the provided value.
    /// <param name="name">The name of the counter to be incremented.</param>
    /// <param name="value">The value to increment the counter by. Defaults to 1.</param>
    public static void IncrementCounter(string name, long value = 1)
    {
        _eventCounters[name] = _eventCounters.GetValueOrDefault(name, 0) + value;
    }

    /// Logs detailed system performance metrics.
    /// This method collects and logs information such as memory usage,
    /// thread count, garbage collection statistics, and uptime.
    /// The information is logged using the provided category and at the Info log level.
    /// <param name="category">The category under which the system information will be logged. Defaults to "System".</param>
    public static void LogSystemInfo(string category = "System")
    {
        var metrics = GetPerformanceMetrics();
        LogWithContext("System Information", new Dictionary<string, object>
        {
            ["TotalMemory"] = $"{metrics.TotalMemory / 1024 / 1024} MB",
            ["UsedMemory"] = $"{metrics.UsedMemory / 1024 / 1024} MB",
            ["ThreadCount"] = metrics.ThreadCount,
            ["GCCollections"] = metrics.GcCollections,
            ["Uptime"] = metrics.Uptime.ToString(@"hh\:mm\:ss")
        }, LogLevel.Info, category);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Creates a log entry object with the specified details, including message, logging level, caller information, and
    ///     additional metadata such as memory usage and elapsed time.
    /// </summary>
    /// <param name="message">The message to log. Can be of any object type, which will be converted to a string.</param>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="category">The category associated with the log entry, used for organizing logs.</param>
    /// <param name="callerMethod">
    ///     The name of the method that called the logging function. Automatically populated by the
    ///     compiler.
    /// </param>
    /// <param name="callerFile">The file name where the logging function was called. Automatically populated by the compiler.</param>
    /// <param name="callerLine">
    ///     The line number in the source file where the logging function was called. Automatically
    ///     populated by the compiler.
    /// </param>
    /// <returns>
    ///     A <see cref="LogEntry" /> object containing all the provided details and system-generated metadata like
    ///     timestamp, memory usage, and elapsed time.
    /// </returns>
    private static LogEntry CreateLogEntry(object message, LogLevel level, string category, string callerMethod,
        string callerFile, int callerLine)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message?.ToString() ?? "null",
            Category = category,
            CallerMethod = callerMethod,
            CallerFile = Path.GetFileName(callerFile),
            CallerLine = callerLine,
            ThreadId = Environment.CurrentManagedThreadId.ToString(),
            ElapsedMs = _globalStopwatch.Elapsed.TotalMilliseconds
        };

        if (_enableMemoryTracking)
            entry.MemoryUsage = GC.GetTotalMemory(false);

        if (_enableStackTrace && level >= LogLevel.Warning)
            entry.StackTrace = new StackTrace(2, true);

        return entry;
    }

    /// Processes a log entry for logging purposes. Determines whether the log entry should
    /// be processed asynchronously or synchronously based on the configuration.
    /// <param name="entry">
    ///     The log entry to be processed. Contains the details such as message, log level,
    ///     category, and additional context or metadata about the logging event.
    /// </param>
    private static void ProcessLogEntry(LogEntry entry)
    {
        if (_enableAsyncLogging)
            _logQueue.Enqueue(entry);
        else
            WriteLogEntry(entry);
    }

    /// Writes a log entry to the appropriate log destinations.
    /// This method processes the given log entry by formatting it and writing it
    /// to different log outputs as determined by its properties and the logging configuration.
    /// <param name="entry">
    ///     The log entry containing information about the log message, such as its level, category, and
    ///     message content.
    /// </param>
    private static void WriteLogEntry(LogEntry entry)
    {
        lock (_lockObject)
        {
            try
            {
                var formattedMessage = FormatLogEntry(entry);

                _fileLog?.WriteLine(formattedMessage);

                if (entry.Level >= LogLevel.Error)
                    _errorLog?.WriteLine(formattedMessage);

                if (entry.Level == LogLevel.Debug || entry.Level == LogLevel.Trace)
                    _debugLog?.WriteLine(formattedMessage);

                if (_enablePerformanceLogging && entry.Category.Contains("Performance"))
                    _performanceLog?.WriteLine(formattedMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BetterLogger write error: {ex.Message}");
            }
        }
    }

    /// Formats a log entry into a structured and detailed string representation.
    /// <param name="entry">
    ///     The log entry to format, containing details such as timestamp, level, message, category, caller
    ///     information, and optional context or exception.
    /// </param>
    /// <return>
    ///     A string containing the formatted log entry, including optional stack trace, memory usage, and exception
    ///     details if available.
    /// </return>
    private static string FormatLogEntry(LogEntry entry)
    {
        _stringBuilder.Clear();

        _stringBuilder.Append($"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] [{entry.Category}] {entry.Message}");
        _stringBuilder.AppendLine();
        _stringBuilder.Append(
            $"  ↳ {entry.CallerFile}:{entry.CallerLine} in {entry.CallerMethod}() | Thread:{entry.ThreadId} | Elapsed:{entry.ElapsedMs:F2}ms");

        if (_enableMemoryTracking)
            _stringBuilder.Append($" | Memory: {entry.MemoryUsage / 1024 / 1024:F2} MB");

        _stringBuilder.AppendLine();

        if (entry.Context.Any())
        {
            _stringBuilder.AppendLine("  ↳ Context:");
            foreach (var kvp in entry.Context)
                _stringBuilder.AppendLine($"    • {kvp.Key}: {JsonSerializer.Serialize(kvp.Value)}");
        }

        if (entry.Exception != null)
        {
            _stringBuilder.AppendLine($"  ↳ Exception: {entry.Exception.GetType().Name}: {entry.Exception.Message}");
            _stringBuilder.AppendLine(
                $"    {entry.Exception.StackTrace?.Replace(Environment.NewLine, $"{Environment.NewLine}    ")}");
        }

        if (entry.StackTrace != null && _enableStackTrace)
        {
            _stringBuilder.AppendLine("  ↳ Stack Trace:");
            var frames = entry.StackTrace.GetFrames();
            if (frames != null)
                for (var i = 0; i < Math.Min(frames.Length, 5); i++)
                {
                    var frame = frames[i];
                    var method = frame.GetMethod();
                    _stringBuilder.AppendLine(
                        $"    {i + 1}. {method?.DeclaringType?.FullName}.{method?.Name} ({Path.GetFileName(frame.GetFileName())}:{frame.GetFileLineNumber()})");
                }
        }

        _stringBuilder.AppendLine();
        return _stringBuilder.ToString();
    }

    /// Retrieves the performance metrics of the current process, including memory usage,
    /// thread count, garbage collection statistics, and uptime.
    /// <returns>
    ///     An instance of the <c>PerformanceMetrics</c> class containing details about
    ///     the system's current performance metrics.
    /// </returns>
    private static PerformanceMetrics GetPerformanceMetrics()
    {
        using var process = Process.GetCurrentProcess();
        return new PerformanceMetrics
        {
            TotalMemory = GC.GetTotalMemory(false),
            UsedMemory = process.WorkingSet64,
            ThreadCount = process.Threads.Count,
            GcCollections = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2),
            Uptime = _globalStopwatch.Elapsed
        };
    }

    /// Flushes the logs from the internal logging queue to their respective outputs.
    /// This method ensures that all queued log entries are processed and written to
    /// the designated log destinations (e.g. files, performance logs, error logs, etc.).
    /// <param name="state">
    ///     An optional state object typically used in timer callbacks;
    ///     this parameter can be null for this method.
    /// </param>
    private static void FlushLogs(object? state)
    {
        try
        {
            while (_logQueue.TryDequeue(out var entry)) WriteLogEntry(entry);

            lock (_lockObject)
            {
                _fileLog?.Flush();
                _performanceLog?.Flush();
                _errorLog?.Flush();
                _debugLog?.Flush();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BetterLogger] CRITICAL ERROR during log flush: {ex}");
        }
    }

    /// Logs performance metrics such as memory usage, garbage collections, thread count, and uptime at regular intervals.
    /// This method is typically used internally by the logging system and controlled by the performance logging toggle.
    /// If performance logging is disabled, this method exits without performing any operations.
    /// <param name="state">An optional state object provided by the Timer callback, which is unused in this implementation.</param>
    private static void LogPerformanceMetrics(object? state)
    {
        if (!_enablePerformanceLogging) return;

        try
        {
            var metrics = GetPerformanceMetrics();
            LogWithContext("Performance Metrics", new Dictionary<string, object>
            {
                ["Memory"] = $"{metrics.UsedMemory / 1024 / 1024} MB",
                ["GC"] = metrics.GcCollections,
                ["Threads"] = metrics.ThreadCount,
                ["Uptime"] = metrics.Uptime.ToString(@"hh\:mm\:ss")
            }, LogLevel.Debug, "Metrics");

            if (_eventCounters.Any())
                LogWithContext("Event Counters", _eventCounters.ToDictionary(k => k.Key, v => (object)v.Value),
                    LogLevel.Debug, "Counters");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BetterLogger] CRITICAL ERROR during performance metric logging: {ex}");
        }
    }

    #endregion

    #region Cleanup

    /// Handles the `ProcessExit` event of the application domain. Performs necessary
    /// cleanup actions such as shutting down the logger and releasing resources.
    /// This method is automatically attached to the `AppDomain.CurrentDomain.ProcessExit` event.
    /// It ensures logging integrity by flushing logs and disposing of timers gracefully.
    /// <param name="sender">The source of the event, typically the application domain.</param>
    /// <param name="e">An `EventArgs` object containing event data.</param>
    private static void OnProcessExit(object? sender, EventArgs e)
    {
        Shutdown();
    }

    /// <summary>
    ///     Shuts down the logger and releases all resources associated with it.
    ///     This method properly disposes of timers, flushes any pending log messages,
    ///     unsubscribes event handlers, closes all log files, and clears references to logging streams.
    /// </summary>
    public static void Shutdown()
    {
        Info("Logger shutting down.", "Shutdown");

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;

        _flushTimer?.Dispose();
        _metricsTimer?.Dispose();

        FlushLogs(null);

        lock (_lockObject)
        {
            _fileLog?.Close();
            _performanceLog?.Close();
            _errorLog?.Close();
            _debugLog?.Close();

            _fileLog = null;
            _performanceLog = null;
            _errorLog = null;
            _debugLog = null;
        }
    }

    #endregion
}