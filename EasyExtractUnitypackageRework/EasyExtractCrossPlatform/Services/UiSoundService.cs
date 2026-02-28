using NAudio.Wave;
using OpenTK.Audio.OpenAL;

namespace EasyExtractCrossPlatform.Services;

public enum UiSoundEffect
{
    None = 0,
    Subtle,
    Positive,
    Negative
}

public sealed class UiSoundService : IDisposable
{
    private static readonly Lazy<UiSoundService> LazyInstance = new(() => new UiSoundService());

    private readonly Dictionary<UiSoundEffect, byte[]> _clipCache = new();
    private readonly object _gate = new();

    private bool _isDisposed;
    private bool _isEnabled = true;
    private OpenAlContext? _openAlContext;
    private float _volume = 1f;

    private UiSoundService()
    {
    }

    public static UiSoundService Instance => LazyInstance.Value;

    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _clipCache.Clear();
            _openAlContext?.Dispose();
            _openAlContext = null;
        }
    }

    public void UpdateSettings(AppSettings settings)
    {
        if (settings is null)
            return;

        UpdateSettings(settings.EnableSound, settings.SoundVolume);
    }

    public void UpdateSettings(bool enabled, double volume)
    {
        lock (_gate)
        {
            _isEnabled = enabled;
            _volume = (float)Math.Clamp(double.IsFinite(volume) ? volume : 1.0, 0.0, 1.0);
        }
    }

    public void Play(UiSoundEffect effect)
    {
        if (effect == UiSoundEffect.None)
            return;

        byte[]? clip;
        float volume;

        lock (_gate)
        {
            if (_isDisposed || !_isEnabled)
                return;

            volume = _volume;
            clip = GetOrLoadClip(effect) ?? (effect == UiSoundEffect.Subtle
                ? null
                : GetOrLoadClip(UiSoundEffect.Subtle));
        }

        if (clip is null || clip.Length == 0)
            return;

        _ = PlayClipAsync(clip, volume);
    }

    private byte[]? GetOrLoadClip(UiSoundEffect effect)
    {
        if (_clipCache.TryGetValue(effect, out var cached))
            return cached;

        var loaded = LoadClip(effect);
        if (loaded is null)
            return null;

        _clipCache[effect] = loaded;
        return loaded;
    }

    private static byte[]? LoadClip(UiSoundEffect effect)
    {
        var assetUri = effect switch
        {
            UiSoundEffect.Positive => "avares://EasyExtractCrossPlatform/Assets/Sounds/ui_success.wav",
            UiSoundEffect.Negative => "avares://EasyExtractCrossPlatform/Assets/Sounds/ui_error.wav",
            UiSoundEffect.Subtle => "avares://EasyExtractCrossPlatform/Assets/Sounds/ui_tap.wav",
            _ => null
        };

        if (assetUri is null)
            return null;

        try
        {
            var uri = new Uri(assetUri);
            if (!AssetLoader.Exists(uri))
            {
                LoggingService.LogWarning($"UI sound asset '{assetUri}' is missing.");
                return null;
            }

            using var stream = AssetLoader.Open(uri);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
        catch (Exception ex)
        {
            LoggingService.LogWarning($"Failed to load UI sound '{assetUri}': {ex.Message}");
            return null;
        }
    }

    private Task PlayClipAsync(byte[] clip, float volume)
    {
        if (OperatingSystem.IsWindows())
            return Task.Run(() => PlayWithWaveOut(clip, volume));

        return Task.Run(() => PlayWithOpenAl(clip, volume));
    }

    private static void PlayWithWaveOut(byte[] clip, float volume)
    {
        try
        {
            using var stream = new MemoryStream(clip, false);
            using var reader = new WaveFileReader(stream);
            using var waveChannel = new WaveChannel32(reader)
            {
                Volume = Math.Clamp(volume, 0f, 1f)
            };
            using var waveOut = new WaveOutEvent
            {
                DesiredLatency = 80
            };

            waveOut.Init(waveChannel);
            waveOut.Play();

            while (waveOut.PlaybackState == PlaybackState.Playing)
                Thread.Sleep(10);
        }
        catch (Exception ex)
        {
            LoggingService.LogWarning($"Failed to play UI sound via WaveOut: {ex.Message}");
        }
    }

    private void PlayWithOpenAl(byte[] clip, float volume)
    {
        OpenAlContext? context;
        lock (_gate)
        {
            context = _openAlContext ??= OpenAlContext.Create();
        }

        if (context is null)
            return;

        lock (context.SyncRoot)
        {
            if (!context.MakeCurrent())
                return;

            try
            {
                using var stream = new MemoryStream(clip, false);
                using var reader = new WaveFileReader(stream);
                if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
                {
                    LoggingService.LogWarning(
                        $"UI sound '{reader.WaveFormat}' uses unsupported encoding {reader.WaveFormat.Encoding}.");
                    return;
                }

                if (!TryGetOpenAlFormat(reader.WaveFormat, out var alFormat))
                {
                    LoggingService.LogWarning(
                        $"UI sound '{reader.WaveFormat}' uses unsupported channel/bit depth combination.");
                    return;
                }

                var totalLength = (int)Math.Min(reader.Length, int.MaxValue);
                var buffer = new byte[totalLength];
                var bytesRead = reader.Read(buffer, 0, totalLength);
                if (bytesRead <= 0)
                    return;

                if (bytesRead < buffer.Length)
                    Array.Resize(ref buffer, bytesRead);

                var bufferId = AL.GenBuffer();
                var sourceId = AL.GenSource();

                try
                {
                    AL.BufferData(bufferId, alFormat, buffer, reader.WaveFormat.SampleRate);
                    AL.Source(sourceId, ALSourcei.Buffer, bufferId);
                    AL.Source(sourceId, ALSourcef.Gain, Math.Clamp(volume, 0f, 1f));
                    AL.SourcePlay(sourceId);

                    AL.GetSource(sourceId, ALGetSourcei.SourceState, out var stateValue);
                    var state = (ALSourceState)stateValue;
                    while (state == ALSourceState.Playing)
                    {
                        Thread.Sleep(10);
                        AL.GetSource(sourceId, ALGetSourcei.SourceState, out stateValue);
                        state = (ALSourceState)stateValue;
                    }
                }
                finally
                {
                    AL.SourceStop(sourceId);
                    AL.DeleteSource(sourceId);
                    AL.DeleteBuffer(bufferId);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"Failed to play UI sound via OpenAL: {ex.Message}");
            }
        }
    }

    private static bool TryGetOpenAlFormat(WaveFormat format, out ALFormat alFormat)
    {
        alFormat = default;

        if (format.BitsPerSample is not (8 or 16))
            return false;

        alFormat = format.Channels switch
        {
            1 => format.BitsPerSample == 8 ? ALFormat.Mono8 : ALFormat.Mono16,
            2 => format.BitsPerSample == 8 ? ALFormat.Stereo8 : ALFormat.Stereo16,
            _ => default
        };

        return alFormat != default;
    }

    private sealed class OpenAlContext : IDisposable
    {
        private OpenAlContext(ALDevice device, ALContext context)
        {
            Device = device;
            Context = context;
        }

        public ALDevice Device { get; }
        public ALContext Context { get; }
        public object SyncRoot { get; } = new();
        private bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            ALC.MakeContextCurrent(ALContext.Null);
            if (Context != ALContext.Null)
                ALC.DestroyContext(Context);

            if (Device != ALDevice.Null)
                ALC.CloseDevice(Device);
        }

        public static OpenAlContext? Create()
        {
            try
            {
                var device = ALC.OpenDevice(null);
                if (device == ALDevice.Null)
                {
                    LoggingService.LogWarning("Unable to open default audio device (OpenAL).");
                    return null;
                }

                var context = ALC.CreateContext(device, Array.Empty<int>());
                if (context == ALContext.Null)
                {
                    LoggingService.LogWarning("Unable to create OpenAL context.");
                    ALC.CloseDevice(device);
                    return null;
                }

                var instance = new OpenAlContext(device, context);
                instance.MakeCurrent();
                return instance;
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"Failed to initialize OpenAL: {ex.Message}");
                return null;
            }
        }

        public bool MakeCurrent()
        {
            if (IsDisposed)
                return false;

            return ALC.MakeContextCurrent(Context);
        }
    }
}