namespace EasyExtractCrossPlatform;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        LoggingService.Initialize();
        LoggingService.LogInformation("EasyExtract starting up.");
        var launchMode = args.Length == 0 ? "Interactive" : "CLI";
        var argsSummary = args.Length == 0 ? "<none>" : string.Join(' ', args);
        LoggingService.LogMode($"Startup mode={launchMode} | args={argsSummary}");

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is Exception exception)
            LoggingService.LogError("Unhandled exception", exception);
        else
            LoggingService.LogError($"Unhandled exception: {eventArgs.ExceptionObject}");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        LoggingService.LogError("Unobserved task exception", eventArgs.Exception);
        eventArgs.SetObserved();
    }
}