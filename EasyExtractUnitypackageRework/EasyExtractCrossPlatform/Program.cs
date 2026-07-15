using System.Text;
using Velopack;

namespace EasyExtractCrossPlatform;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        LoggingService.Initialize();
        LoggingService.LogInformation("EasyExtract starting up.");
        var launchMode = args.Length == 0 ? "Interactive" : "CLI";
        var argsSummary = args.Length == 0 ? "<none>" : string.Join(' ', args);
        LoggingService.LogMode($"Startup mode={launchMode} | args={argsSummary}");

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            FatalCrashHandler.Handle(exception, "Fatal exception escaped the application lifetime.");
            Environment.Exit(1);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        if (OperatingSystem.IsLinux())
            builder.With(new X11PlatformOptions
            {
                UseDBusMenu = false,
                UseDBusFilePicker = false
            });

        return builder;
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs eventArgs)
    {
        try
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                // Free some headroom first so formatting and forwarding the report can succeed.
                if (FatalCrashHandler.IsOutOfMemory(exception))
                    GC.Collect();

                LoggingService.LogError("Unhandled exception", exception);
            }
            else
            {
                LoggingService.LogError($"Unhandled exception: {eventArgs.ExceptionObject}");
            }
        }
        catch
        {
            // The process is going down; never throw from the crash reporter.
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        try
        {
            LoggingService.LogError("Unobserved task exception", eventArgs.Exception);
        }
        catch
        {
            // Reporting is best effort; observing the exception below is what matters.
        }

        eventArgs.SetObserved();
    }
}