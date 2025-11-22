namespace EasyExtractCrossPlatform.ViewModels;

public sealed partial class UnityPackagePreviewViewModel
{
    private string? _securityErrorText;
    private bool _securityScanFailed;
    private bool _securityScanInProgress;
    private MaliciousCodeScanResult? _securityScanResult;
    private Task? _securityScanTask;
    private string? _securityStatusText = "Security scan pending...";

    public ObservableCollection<SecurityThreatDisplay> SecurityThreats { get; } = new();

    public bool SecurityScanInProgress
    {
        get => _securityScanInProgress;
        private set
        {
            if (value == _securityScanInProgress)
                return;

            _securityScanInProgress = value;
            OnPropertyChanged(nameof(SecurityScanInProgress));
            OnPropertyChanged(nameof(ShowSecuritySection));
        }
    }

    public bool SecurityScanFailed
    {
        get => _securityScanFailed;
        private set
        {
            if (value == _securityScanFailed)
                return;

            _securityScanFailed = value;
            OnPropertyChanged(nameof(SecurityScanFailed));
            OnPropertyChanged(nameof(ShowSecuritySection));
        }
    }

    public string? SecurityStatusText
    {
        get => _securityStatusText;
        private set
        {
            if (string.Equals(_securityStatusText, value, StringComparison.Ordinal))
                return;

            _securityStatusText = value;
            OnPropertyChanged(nameof(SecurityStatusText));
        }
    }

    public string? SecurityErrorText
    {
        get => _securityErrorText;
        private set
        {
            if (string.Equals(_securityErrorText, value, StringComparison.Ordinal))
                return;

            _securityErrorText = value;
            OnPropertyChanged(nameof(SecurityErrorText));
            OnPropertyChanged(nameof(HasSecurityError));
            OnPropertyChanged(nameof(ShowSecuritySection));
        }
    }

    public bool SecurityHasThreats => SecurityThreats.Count > 0;

    public bool HasSecurityError => !string.IsNullOrWhiteSpace(SecurityErrorText);

    public bool ShowSecuritySection =>
        SecurityScanInProgress || SecurityScanFailed || SecurityHasThreats || HasSecurityError;

    private Task? RunSecurityScanAsync()
    {
        if (_securityScanProvider is null)
            return null;

        return Task.Run(async () =>
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SecurityScanInProgress = true;
                    SecurityScanFailed = false;
                    SecurityErrorText = null;
                    SecurityStatusText = "Scanning for malicious code...";
                }, DispatcherPriority.Background);

                var result = await _securityScanProvider().ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(
                    () => ApplySecurityScanResult(result),
                    DispatcherPriority.Background);
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SecurityScanInProgress = false;
                    SecurityStatusText = "Security scan cancelled.";
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Security scan failed for preview '{PackagePath}'.", ex);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SecurityScanInProgress = false;
                    SecurityScanFailed = true;
                    SecurityStatusText = "Security scan failed.";
                    SecurityErrorText = "Unable to determine if the package is safe.";
                }, DispatcherPriority.Background);
            }
        }, _disposeCts.Token);
    }

    private void ApplySecurityScanResult(MaliciousCodeScanResult? result)
    {
        _securityScanResult = result;
        SecurityThreats.Clear();

        if (result?.Threats is { Count: > 0 })
        {
            foreach (var threat in result.Threats)
                SecurityThreats.Add(SecurityThreatDisplay.FromThreat(threat));

            SecurityStatusText = "Potentially malicious content detected.";
            SecurityScanFailed = false;
            SecurityErrorText = null;
        }
        else if (result is not null)
        {
            SecurityStatusText = "No malicious code detected.";
            SecurityScanFailed = false;
            SecurityErrorText = null;
        }
        else
        {
            SecurityStatusText = "Security scan unavailable.";
            SecurityScanFailed = true;
            SecurityErrorText = "This package could not be scanned.";
        }

        SecurityScanInProgress = false;
    }

    private void OnSecurityThreatsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SecurityHasThreats));
        OnPropertyChanged(nameof(ShowSecuritySection));
    }
}