using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using EasyExtractCrossPlatform.Tests.Support;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApplication))]

namespace EasyExtractCrossPlatform.Tests.Support;

/// <summary>
///     Minimal headless host for control tests. It pulls in the real search bar style so tests
///     exercise the shipped control template without booting the full application.
/// </summary>
public sealed class HeadlessTestApplication : Application
{
    private const string SearchBarStyleUri = "avares://EasyExtractCrossPlatform/Styles/SearchBarStyle.axaml";

    /// <summary>
    ///     Picked up by AppBuilder.Configure(Type). Real Skia rendering instead of the headless
    ///     drawing stub, so tests that decode images see the same pixels production does.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<HeadlessTestApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            });
    }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri(SearchBarStyleUri))
        {
            Source = new Uri(SearchBarStyleUri)
        });
    }
}

/// <summary>
///     Runs a test body on the headless UI thread. The Avalonia.Headless.XUnit adapter needs
///     xunit v3, so the session is driven directly instead.
/// </summary>
public static class HeadlessUi
{
    public static Task RunAsync(Action body)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        return session.Dispatch(body, CancellationToken.None);
    }

    public static Task RunAsync(Func<Task> body)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        return session.Dispatch(async () =>
        {
            await body().ConfigureAwait(true);
            return true;
        }, CancellationToken.None);
    }
}
