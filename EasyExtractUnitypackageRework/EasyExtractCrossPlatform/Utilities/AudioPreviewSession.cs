using System;
using System.IO;
using NAudio.Wave;

namespace EasyExtractCrossPlatform.Utilities;

public sealed class AudioPreviewSession : IDisposable
{
    private readonly byte[] _data;
    private readonly string _extension;
    private bool _manualStopRequested;
    private WaveStream? _reader;
    private MemoryStream? _stream;
    private IWavePlayer? _waveOut;

    private AudioPreviewSession(byte[] data, string extension)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _extension = extension ?? string.Empty;
    }

    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

    public bool IsCompleted { get; private set; }

    public bool CanSeek => _reader?.CanSeek ?? false;

    public TimeSpan CurrentTime => _reader?.CurrentTime ?? TimeSpan.Zero;

    public TimeSpan TotalTime => _reader?.TotalTime ?? TimeSpan.Zero;

    public void Dispose()
    {
        if (_waveOut is not null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Dispose();
            _waveOut = null;
        }

        _reader?.Dispose();
        _reader = null;

        _stream?.Dispose();
        _stream = null;
    }

    public event EventHandler<AudioPlaybackStoppedEventArgs>? PlaybackStopped;

    public static bool SupportsExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        return extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".aiff", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".aif", StringComparison.OrdinalIgnoreCase);
    }

    public static AudioPreviewSession? TryCreate(byte[] data, string extension)
    {
        if (!SupportsExtension(extension))
            return null;

        var session = new AudioPreviewSession(data, extension);
        return session.Initialize() ? session : null;
    }

    private bool Initialize()
    {
        try
        {
            _stream = new MemoryStream(_data, false);
            _reader = CreateReader(_stream, _extension);
            if (_reader is null)
                return false;

            _waveOut = new WaveOutEvent { DesiredLatency = 120 };
            _waveOut.Init(_reader);
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            return true;
        }
        catch
        {
            Dispose();
            return false;
        }
    }

    private static WaveStream? CreateReader(Stream source, string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".wav" => new WaveFileReader(source),
            ".mp3" => new Mp3FileReader(source),
            ".aiff" or ".aif" => new AiffFileReader(source),
            _ => null
        };
    }

    public void Play()
    {
        if (_waveOut is null || _reader is null)
            return;

        if (IsCompleted)
            RewindInternal();

        if (_waveOut.PlaybackState != PlaybackState.Playing)
            _waveOut.Play();
    }

    public void Pause()
    {
        if (_waveOut?.PlaybackState == PlaybackState.Playing)
            _waveOut.Pause();
    }

    public void Stop()
    {
        if (_waveOut is null)
            return;

        _manualStopRequested = true;
        _waveOut.Stop();
    }

    public void Rewind()
    {
        RewindInternal();
    }

    private void RewindInternal()
    {
        if (_reader is null)
            return;

        _reader.Position = 0;
        IsCompleted = false;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        var completed = !_manualStopRequested && _reader is not null &&
                        Math.Abs((_reader.TotalTime - _reader.CurrentTime).TotalSeconds) < 0.02;

        if (!completed)
            RewindInternal();
        else
            IsCompleted = true;

        _manualStopRequested = false;
        PlaybackStopped?.Invoke(this, new AudioPlaybackStoppedEventArgs(completed));
    }
}

public sealed class AudioPlaybackStoppedEventArgs : EventArgs
{
    public AudioPlaybackStoppedEventArgs(bool completed)
    {
        Completed = completed;
    }

    public bool Completed { get; }
}