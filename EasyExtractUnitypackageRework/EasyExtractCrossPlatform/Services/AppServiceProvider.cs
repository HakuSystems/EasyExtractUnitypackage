using System.Collections.Concurrent;

namespace EasyExtractCrossPlatform.Services;

public interface IAppServiceProvider
{
    T GetRequiredService<T>() where T : class;
}

public sealed class AppServiceProvider : IAppServiceProvider
{
    private readonly ConcurrentDictionary<Type, Lazy<object>> _registrations = new();

    public TService GetRequiredService<TService>() where TService : class
    {
        if (_registrations.TryGetValue(typeof(TService), out var lazy) && lazy.Value is TService service)
            return service;

        throw new InvalidOperationException(
            $"Service '{typeof(TService).FullName}' has not been registered.");
    }

    public AppServiceProvider RegisterSingleton<TService>(Func<TService> factory) where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        var lazy = new Lazy<object>(() =>
        {
            var instance = factory();
            if (instance is null)
                throw new InvalidOperationException(
                    $"The factory for service type '{typeof(TService).FullName}' returned null.");

            return instance;
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        if (!_registrations.TryAdd(typeof(TService), lazy))
            throw new InvalidOperationException(
                $"A registration for '{typeof(TService).FullName}' already exists.");

        return this;
    }
}

public static class AppServiceLocator
{
    private static readonly Lazy<IAppServiceProvider> LazyProvider =
        new(CreateDefaultProvider, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IAppServiceProvider Current => LazyProvider.Value;

    private static IAppServiceProvider CreateDefaultProvider()
    {
        var provider = new AppServiceProvider();

        provider
            .RegisterSingleton(CreateSearchServiceForCurrentPlatform)
            .RegisterSingleton<IUnityPackageExtractionService>(() => new UnityPackageExtractionService())
            .RegisterSingleton<IMaliciousCodeDetectionService>(() => new MaliciousCodeDetectionService())
            .RegisterSingleton<INotificationService>(() => new NotificationService())
            .RegisterSingleton<IUnityPackagePreviewService>(() => new UnityPackagePreviewService())
            .RegisterSingleton<IUpdateService>(() => UpdateService.Instance);

        return provider;
    }

    private static IEverythingSearchService CreateSearchServiceForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return new EverythingSearchService();

        if (OperatingSystem.IsMacOS())
            return new SpotlightSearchService();

        if (OperatingSystem.IsLinux())
            return new LinuxSearchService();

        return new UnsupportedSearchService(Environment.OSVersion.Platform.ToString());
    }
}