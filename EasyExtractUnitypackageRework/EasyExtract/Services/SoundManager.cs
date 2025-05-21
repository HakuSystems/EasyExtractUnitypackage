using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Utilities;
using NAudio.Wave;

namespace EasyExtract.Services;

public class SoundManager
{
    private AudioFileReader? _audioFileReader;
    private IWavePlayer? _waveOutDevice;

    public async Task PlayAudio(string? audioFilePath)
    {
        try
        {
            DisposeWave();

            if (!ConfigHandler.Instance.Config.EnableSound) return;

            var tempFilePath = await GetSoundFilePath(audioFilePath);

            if (string.IsNullOrEmpty(tempFilePath))
            {
                await BetterLogger.LogAsync("Audio file path is null or empty after GetSoundFilePath.",
                    Importance.Warning);
                return;
            }

            _waveOutDevice = new WaveOut();
            _audioFileReader = new AudioFileReader(tempFilePath);

            _audioFileReader.Volume = ConfigHandler.Instance.Config.SoundVolume;
            _waveOutDevice.Init(_audioFileReader);
            _waveOutDevice.Play();
        }
        catch (Exception)
        {
            ResetAudio();
        }
    }

    private void ResetAudio()
    {
        DisposeWave();
        _ = DialogHelper.ShowInfoDialogAsync(null, "Audio Error", "Audio playback failed");
    }

    private static async Task<string?> GetSoundFilePath(string? audioFilePath)
    {
        // If audioFilePath is not null, start with pack://, get resource from pack URI
        if (audioFilePath != null && audioFilePath.StartsWith("pack://"))
            return await GetResourceFromPackUri(audioFilePath);

        // If audioFilePath is a valid file, return it
        if (File.Exists(audioFilePath)) return audioFilePath;

        // If nothing is appropriate, throw an exception
        await BetterLogger.LogAsync($"Audio file not found: {audioFilePath}", Importance.Warning);
        throw new FileNotFoundException($"Audio file not found: {audioFilePath}");
    }

    private static async Task<string?> GetResourceFromPackUri(string? packUri)
    {
        if (packUri != null)
        {
            var uri = new Uri(packUri, UriKind.RelativeOrAbsolute);
            var streamResourceInfo = Application.GetResourceStream(uri);
            if (streamResourceInfo == null)
            {
                await BetterLogger.LogAsync($"Failed to find resource: {packUri}", Importance.Warning);
                return null;
            }

            var tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var stream = streamResourceInfo.Stream;
            using var fileStream = File.Open(tempFilePath, FileMode.Create);
            stream.CopyTo(fileStream);

            return tempFilePath;
        }

        return null;
    }

    private void DisposeWave()
    {
        _waveOutDevice?.Stop();
        _waveOutDevice?.Dispose();
        _waveOutDevice = null;

        _audioFileReader?.Dispose();
        _audioFileReader = null;
    }
}