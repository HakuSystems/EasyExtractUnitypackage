using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Vorbis;
using NAudio.Wave;

namespace EasyExtractCrossPlatform.Utilities;

public sealed class AudioPreviewSession : IDisposable
{
    private const string TempFilePrefix = "EasyExtractAudioPreview";

    private static readonly HashSet<string> KnownAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".wave", ".mp3", ".mp2", ".aiff", ".aif", ".aiffc", ".ogg", ".oga", ".flac", ".m4a", ".aac", ".wma",
        ".opus", ".caf", ".au", ".mka", ".mpa"
    };

    private static readonly HashSet<string> StreamCapableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".wave", ".mp3", ".aiff", ".aif", ".ogg", ".oga"
    };

    private readonly byte[]? _data;
    private readonly string _extension;
    private readonly string? _filePath;
    private readonly bool _ownsTempFile;
    private bool _manualStopRequested;
    private WaveStream? _reader;
    private Stream? _stream;
    private IWavePlayer? _waveOut;

    private AudioPreviewSession(byte[]? data, string extension, string? filePath, bool ownsTempFile)
    {
        _data = data;
        _extension = extension;
        _filePath = filePath;
        _ownsTempFile = ownsTempFile;
    }

    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

    public bool IsCompleted { get; private set; }

    public bool CanSeek => _reader?.CanSeek ?? false;

    public TimeSpan CurrentTime => _reader?.CurrentTime ?? TimeSpan.Zero;

    public TimeSpan TotalTime => _reader?.TotalTime ?? TimeSpan.Zero;

    public static IReadOnlyCollection<string> KnownExtensions => KnownAudioExtensions;

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

        if (_ownsTempFile && !string.IsNullOrWhiteSpace(_filePath))
            TryDeleteFile(_filePath);
    }

    public event EventHandler<AudioPlaybackStoppedEventArgs>? PlaybackStopped;

    public static bool Supports(string? extension, bool hasFile)
    {
        var normalized = NormalizeExtension(extension);
        return SupportsInternal(normalized, hasFile);
    }

    public static AudioPreviewSession? TryCreate(byte[]? data, string extension, string? filePath = null)
    {
        var normalizedExtension = NormalizeExtension(extension);
        var hasFile = !string.IsNullOrWhiteSpace(filePath);
        var hasData = data is { Length: > 0 };

        if (!hasFile && !hasData)
            return null;

        if (!SupportsInternal(normalizedExtension, hasFile))
            return null;

        var ownsTempFile = false;
        var buffer = hasFile ? null : data;
        var effectiveFilePath = filePath;

        if (!hasFile && hasData && RequiresFilePlayback(normalizedExtension))
        {
            effectiveFilePath = WriteToTemporaryFile(data!, normalizedExtension);
            if (effectiveFilePath is null)
                return null;

            ownsTempFile = true;
            buffer = null;
            hasFile = true;
        }

        var session = new AudioPreviewSession(buffer, normalizedExtension, effectiveFilePath, ownsTempFile);
        return session.Initialize() ? session : null;
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

    private static bool SupportsInternal(string normalizedExtension, bool hasFile)
    {
        if (hasFile)
            return true;

        return !string.IsNullOrEmpty(normalizedExtension) && KnownAudioExtensions.Contains(normalizedExtension);
    }

    private bool Initialize()
    {
        try
        {
            _reader = CreateReader();
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

    private WaveStream? CreateReader()
    {
        if (!string.IsNullOrWhiteSpace(_filePath))
            return CreateReaderFromFile(_filePath!, _extension);

        if (_data is null)
            return null;

        _stream = new MemoryStream(_data, false);
        return CreateReaderFromStream(_stream, _extension);
    }

    private static WaveStream? CreateReaderFromStream(Stream source, string extension)
    {
        try
        {
            return extension switch
            {
                ".wav" or ".wave" => new WaveFileReader(source),
                ".mp3" => new Mp3FileReader(source),
                ".aiff" or ".aif" or ".aiffc" => new AiffFileReader(source),
                ".ogg" or ".oga" => new VorbisWaveReader(source),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static WaveStream? CreateReaderFromFile(string filePath, string extension)
    {
        try
        {
            return extension switch
            {
                ".wav" or ".wave" => new WaveFileReader(filePath),
                ".mp3" => new Mp3FileReader(filePath),
                ".aiff" or ".aif" or ".aiffc" => new AiffFileReader(filePath),
                ".ogg" or ".oga" => new VorbisWaveReader(filePath),
                _ => new AudioFileReader(filePath)
            };
        }
        catch
        {
            try
            {
                return new AudioFileReader(filePath);
            }
            catch
            {
                return null;
            }
        }
    }

    private static bool RequiresFilePlayback(string extension)
    {
        return string.IsNullOrEmpty(extension) || !StreamCapableExtensions.Contains(extension);
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        var trimmed = extension.Trim();
        if (!trimmed.StartsWith(".", StringComparison.Ordinal))
            trimmed = "." + trimmed;

        return trimmed.ToLowerInvariant();
    }

    private static string? WriteToTemporaryFile(byte[] data, string extension)
    {
        try
        {
            var safeExtension = string.IsNullOrEmpty(extension) ? ".audio" : extension;
            var path = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}_{Guid.NewGuid():N}{safeExtension}");
            File.WriteAllBytes(path, data);
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ignore cleanup failures; OS can remove temp files later.
        }
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