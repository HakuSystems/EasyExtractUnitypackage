using System;
using Avalonia.Threading;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.ViewModels;

public sealed partial class UnityPackagePreviewViewModel
{
    private string _audioDurationText = "00:00";
    private string _audioPositionText = "00:00";
    private AudioPreviewSession? _audioPreviewSession;
    private double _audioProgress;
    private string _audioStatusText = "Ready";
    private DispatcherTimer? _audioTimer;

    public bool HasAudioPreview => _audioPreviewSession is not null;

    public bool IsAudioPlaying => _audioPreviewSession?.IsPlaying ?? false;

    public string AudioStatusText
    {
        get => _audioStatusText;
        private set
        {
            if (string.Equals(_audioStatusText, value, StringComparison.Ordinal))
                return;

            _audioStatusText = value;
            OnPropertyChanged(nameof(AudioStatusText));
        }
    }

    public string AudioPositionText
    {
        get => _audioPositionText;
        private set
        {
            if (string.Equals(_audioPositionText, value, StringComparison.Ordinal))
                return;

            _audioPositionText = value;
            OnPropertyChanged(nameof(AudioPositionText));
        }
    }

    public string AudioDurationText
    {
        get => _audioDurationText;
        private set
        {
            if (string.Equals(_audioDurationText, value, StringComparison.Ordinal))
                return;

            _audioDurationText = value;
            OnPropertyChanged(nameof(AudioDurationText));
        }
    }

    public double AudioProgress
    {
        get => _audioProgress;
        private set
        {
            if (Math.Abs(_audioProgress - value) < 0.0001)
                return;

            _audioProgress = value;
            OnPropertyChanged(nameof(AudioProgress));
        }
    }

    public bool CanSeekAudio => _audioPreviewSession?.CanSeek ?? false;

    public RelayCommand PlayAudioPreviewCommand { get; }

    public RelayCommand StopAudioPreviewCommand { get; }

    private void TryCreateAudioPreview(UnityPackageAssetPreviewItem asset)
    {
        ResetAudioPreview();

        var hasData = asset.AssetData is { Length: > 0 };
        var hasFile = !string.IsNullOrWhiteSpace(asset.AssetFilePath);

        if (!hasData && !hasFile)
            return;

        if (!AudioPreviewSession.Supports(asset.Extension, hasFile))
            return;

        var session = AudioPreviewSession.TryCreate(asset.AssetData, asset.Extension, asset.AssetFilePath);
        if (session is null)
        {
            AudioStatusText = "Audio preview unsupported";
            LoggingService.LogInformation($"Audio preview unsupported for '{asset.RelativePath}'.");
            return;
        }

        _audioPreviewSession = session;
        _audioPreviewSession.PlaybackStopped += HandleAudioPlaybackStopped;
        AudioDurationText = FormatTime(session.TotalTime);
        AudioPositionText = FormatTime(TimeSpan.Zero);
        AudioStatusText = "Ready";
        AudioProgress = 0;
        LoggingService.LogInformation(
            $"Audio preview ready for '{asset.RelativePath}'. Duration={session.TotalTime}.");
    }

    private void ResetAudioPreview()
    {
        StopAudioTimer();
        if (_audioPreviewSession is null)
            return;

        _audioPreviewSession.PlaybackStopped -= HandleAudioPlaybackStopped;
        _audioPreviewSession.Dispose();
        _audioPreviewSession = null;
        LoggingService.LogInformation("Audio preview session reset.");
    }

    private void ToggleAudioPlayback()
    {
        if (_audioPreviewSession is null)
            return;

        if (_audioPreviewSession.IsPlaying)
        {
            _audioPreviewSession.Pause();
            AudioStatusText = "Paused";
            StopAudioTimer();
            LoggingService.LogInformation("Audio preview paused.");
        }
        else
        {
            if (_audioPreviewSession.IsCompleted)
                _audioPreviewSession.Rewind();

            _audioPreviewSession.Play();
            AudioStatusText = "Playing";
            StartAudioTimer();
            LoggingService.LogInformation("Audio preview playback started.");
        }

        OnPropertyChanged(nameof(IsAudioPlaying));
        AudioPositionText = FormatTime(_audioPreviewSession.CurrentTime);
        AudioDurationText = FormatTime(_audioPreviewSession.TotalTime);
    }

    private void StopAudioPlayback()
    {
        if (_audioPreviewSession is null)
            return;

        _audioPreviewSession.Stop();
        AudioStatusText = "Stopped";
        StopAudioTimer();
        AudioPositionText = FormatTime(TimeSpan.Zero);
        AudioProgress = 0;
        OnPropertyChanged(nameof(IsAudioPlaying));
        LoggingService.LogInformation("Audio preview stopped.");
    }

    private void HandleAudioPlaybackStopped(object? sender, AudioPlaybackStoppedEventArgs e)
    {
        StopAudioTimer();
        if (_audioPreviewSession is null)
            return;

        AudioPositionText = FormatTime(_audioPreviewSession.CurrentTime);
        AudioDurationText = FormatTime(_audioPreviewSession.TotalTime);
        AudioProgress = e.Completed && _audioPreviewSession.TotalTime.TotalSeconds > 0
            ? 1
            : 0;
        if (!e.Completed)
            AudioProgress = 0;
        AudioStatusText = e.Completed ? "Completed" : "Stopped";
        OnPropertyChanged(nameof(IsAudioPlaying));
        LoggingService.LogInformation(
            $"Audio playback stopped. Completed={e.Completed}, Position={_audioPreviewSession.CurrentTime}.");
    }

    private void StartAudioTimer()
    {
        _audioTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _audioTimer.Tick -= OnAudioTimerTick;
        _audioTimer.Tick += OnAudioTimerTick;
        _audioTimer.Start();
    }

    private void StopAudioTimer()
    {
        if (_audioTimer is null)
            return;

        _audioTimer.Stop();
        _audioTimer.Tick -= OnAudioTimerTick;
    }

    private void OnAudioTimerTick(object? sender, EventArgs e)
    {
        if (_audioPreviewSession is null)
        {
            StopAudioTimer();
            return;
        }

        AudioPositionText = FormatTime(_audioPreviewSession.CurrentTime);
        AudioDurationText = FormatTime(_audioPreviewSession.TotalTime);
        var totalSeconds = _audioPreviewSession.TotalTime.TotalSeconds;
        AudioProgress = totalSeconds > 0
            ? Math.Clamp(_audioPreviewSession.CurrentTime.TotalSeconds / totalSeconds, 0, 1)
            : 0;

        OnPropertyChanged(nameof(IsAudioPlaying));
    }

    private void UpdateAudioCommands()
    {
        PlayAudioPreviewCommand.RaiseCanExecuteChanged();
        StopAudioPreviewCommand.RaiseCanExecuteChanged();
    }

    private static string FormatTime(TimeSpan timeSpan)
    {
        return timeSpan.TotalHours >= 1
            ? timeSpan.ToString(@"hh\:mm\:ss")
            : timeSpan.ToString(@"mm\:ss");
    }
}
