namespace EasyExtractCrossPlatform.Utilities;

internal static class DeferredTextBoxShortcutHandler
{
    internal static IDisposable Attach(TextBox textBox, params DeferredTextBoxShortcut[] shortcuts)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        ArgumentNullException.ThrowIfNull(shortcuts);

        if (shortcuts.Length == 0)
            return NoopDisposable.Instance;

        EventHandler<KeyEventArgs>? handler = null;
        handler = (_, e) =>
        {
            if (e.Handled)
                return;

            foreach (var shortcut in shortcuts)
            {
                if (!shortcut.Matches(e))
                    continue;

                if (!shortcut.CanExecute())
                    return;

                e.Handled = true;
                Dispatcher.UIThread.Post(() =>
                {
                    if (shortcut.CanExecute())
                        shortcut.Execute();
                }, DispatcherPriority.Background);
                return;
            }
        };

        textBox.KeyDown += handler;
        return new DelegateDisposable(() => textBox.KeyDown -= handler);
    }

    internal readonly struct DeferredTextBoxShortcut
    {
        private readonly Func<bool> _canExecute;
        private readonly Action _execute;

        internal DeferredTextBoxShortcut(Key key, Func<bool> canExecute, Action execute)
            : this(key, KeyModifiers.None, canExecute, execute)
        {
        }

        internal DeferredTextBoxShortcut(Key key, KeyModifiers modifiers, Func<bool> canExecute, Action execute)
        {
            ArgumentNullException.ThrowIfNull(canExecute);
            ArgumentNullException.ThrowIfNull(execute);

            Key = key;
            Modifiers = modifiers;
            _canExecute = canExecute;
            _execute = execute;
        }

        internal Key Key { get; }

        internal KeyModifiers Modifiers { get; }

        internal bool Matches(KeyEventArgs e)
        {
            return e.Key == Key && e.KeyModifiers == Modifiers;
        }

        internal bool CanExecute()
        {
            return _canExecute();
        }

        internal void Execute()
        {
            _execute();
        }
    }

    private sealed class DelegateDisposable(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        internal static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}