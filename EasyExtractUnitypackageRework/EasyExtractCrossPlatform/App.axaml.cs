using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EasyExtractCrossPlatform.Services;

namespace EasyExtractCrossPlatform;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (OperatingSystem.IsWindows())
        {
            _ = EverythingSdkBootstrapper.EnsureInitializedAsync()
                .ContinueWith(task =>
                {
                    if (task.Exception is not null)
                        Debug.WriteLine($"Failed to initialize Everything SDK: {task.Exception}");
                }, TaskScheduler.Default);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}