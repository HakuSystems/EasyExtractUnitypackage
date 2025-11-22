namespace EasyExtractCrossPlatform.Utilities;

public static class ResponsiveWindowHelper
{
    private const double DefaultCompactBreakpoint = 1200;
    private const double DefaultCozyBreakpoint = 1600;

    public static IDisposable Enable(Window window, double compactBreakpoint = DefaultCompactBreakpoint,
        double cozyBreakpoint = DefaultCozyBreakpoint)
    {
        if (window is null)
            throw new ArgumentNullException(nameof(window));

        void Update(Size size)
        {
            if (size.Width <= 0)
                return;

            ApplyClass(window.Classes, "compact", size.Width < compactBreakpoint);
            ApplyClass(window.Classes, "cozy", size.Width >= compactBreakpoint && size.Width < cozyBreakpoint);
            ApplyClass(window.Classes, "wide", size.Width >= cozyBreakpoint);
        }

        var subscription = window.GetObservable(Visual.BoundsProperty)
            .Subscribe(new ActionObserver<Rect>(bounds => Update(bounds.Size)));

        Update(window.Bounds.Size);

        return new ActionDisposable(subscription.Dispose);
    }

    private static void ApplyClass(Classes classes, string className, bool isActive)
    {
        if (classes.Contains(className) == isActive)
            return;

        if (isActive)
            classes.Add(className);
        else
            classes.Remove(className);
    }

    private sealed class ActionDisposable : IDisposable
    {
        private readonly Action _dispose;
        private bool _isDisposed;

        public ActionDisposable(Action dispose)
        {
            _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _dispose();
        }
    }

    private sealed class ActionObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;

        public ActionObserver(Action<T> onNext)
        {
            _onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(T value)
        {
            _onNext(value);
        }
    }
}