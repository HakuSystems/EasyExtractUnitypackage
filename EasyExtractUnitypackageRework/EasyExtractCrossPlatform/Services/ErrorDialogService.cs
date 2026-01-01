using Avalonia.Controls.ApplicationLifetimes;

namespace EasyExtractCrossPlatform.Services;

public interface IErrorDialogService : IDisposable
{
    Task ShowErrorAsync(string message, string? detail = null, string? title = null);
}

public sealed class ErrorDialogService : IErrorDialogService
{
    private readonly object _gate = new();
    private readonly Queue<PendingError> _pending = new();
    private bool _processing;

    public ErrorDialogService()
    {
        LoggingService.ErrorLogged += OnErrorLogged;
    }

    public Task ShowErrorAsync(string message, string? detail = null, string? title = null)
    {
        var resolvedTitle = string.IsNullOrWhiteSpace(title) ? "EasyExtract ran into a problem" : title;
        var resolvedMessage = string.IsNullOrWhiteSpace(message) ? "An unexpected error occurred." : message;
        Enqueue(new PendingError(resolvedTitle, resolvedMessage, NormalizeDetail(detail)));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        LoggingService.ErrorLogged -= OnErrorLogged;
    }

    private void OnErrorLogged(object? sender, LogEntryEventArgs args)
    {
        var detail = NormalizeDetail(args.Exception?.ToString()) ?? args.FormattedPayload;
        var message = string.IsNullOrWhiteSpace(args.Message)
            ? "An unexpected error occurred."
            : args.Message;

        Enqueue(new PendingError("EasyExtract ran into a problem", message, detail));
    }

    private void Enqueue(PendingError error)
    {
        lock (_gate)
        {
            _pending.Enqueue(error);
            if (_processing)
                return;

            _processing = true;
        }

        _ = ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            PendingError? next;
            lock (_gate)
            {
                if (_pending.Count == 0)
                {
                    _processing = false;
                    return;
                }

                next = _pending.Dequeue();
            }

            await ShowDialogAsync(next.Value);
        }
    }

    private static async Task ShowDialogAsync(PendingError error)
    {
        // No UI available (CLI mode) — skip showing dialogs.
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime)
            return;

        var operation = Dispatcher.UIThread.InvokeAsync<Task>(() =>
        {
            var window = new ErrorDialogWindow(error.Title, error.Message, error.Detail);

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow is { } owner)
                return window.ShowDialog(owner);

            window.Show();
            var completion = new TaskCompletionSource();
            window.Closed += (_, _) => completion.TrySetResult();
            return completion.Task;
        });

        var dialogTask = await operation;
        await dialogTask;
    }

    private static string? NormalizeDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return null;

        return detail.Trim();
    }

    private readonly record struct PendingError(string Title, string Message, string? Detail);
}