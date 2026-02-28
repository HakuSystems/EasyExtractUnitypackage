using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class AppSettingsServiceTests : IDisposable
{
  private readonly string _rootDirectory;
  private readonly string _settingsDirectory;
  private readonly string _settingsFilePath;

  public AppSettingsServiceTests()
  {
    _rootDirectory = Path.Combine(
      Path.GetTempPath(),
      "EasyExtractTests",
      nameof(AppSettingsServiceTests),
      Guid.NewGuid().ToString("N"));
    _settingsDirectory = Path.Combine(_rootDirectory, "Config");
    _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");

    Directory.CreateDirectory(_settingsDirectory);
    AppSettingsService.ResetForTests();
    AppSettingsService.ConfigureForTests(_settingsDirectory, _settingsFilePath);
  }

  public void Dispose()
  {
    AppSettingsService.ResetForTests();

    if (Directory.Exists(_rootDirectory))
      Directory.Delete(_rootDirectory, true);
  }

    [Fact]
    public void DeserializeForTests_ObjectValueInFilePath_DoesNotFallbackToDefaults()
    {
        const string json = """
                            {
                              "DefaultOutputPath": "C:\\custom-output",
                              "TotalExtracted": 42,
                              "ExtractedUnitypackages": [
                                {
                                  "FilePath": {
                                    "legacy": "value"
                                  },
                                  "FileName": "Legacy Package",
                                  "DateExtracted": "2026-01-01T10:00:00Z"
                                },
                                {
                                  "FilePath": "C:\\packs\\one.unitypackage",
                                  "DateExtracted": "2026-01-02T10:00:00Z"
                                }
                              ]
                            }
                            """;

        var settings = AppSettingsService.DeserializeForTests(json);

        Assert.Equal(@"C:\custom-output", settings.DefaultOutputPath);
        Assert.Equal(42, settings.TotalExtracted);
        Assert.Equal(2, settings.ExtractedUnitypackages.Count);
        Assert.Equal("Legacy Package", settings.ExtractedUnitypackages[0].FileName);
        Assert.Equal(string.Empty, settings.ExtractedUnitypackages[0].FilePath);
        Assert.Equal(@"C:\packs\one.unitypackage", settings.ExtractedUnitypackages[1].FilePath);
    }

    [Fact]
    public void DeserializeForTests_MixedLegacyAndMalformedEntries_ConvertsAndSkipsInvalid()
    {
        const string json = """
                            {
                              "ExtractedUnitypackages": [
                                "C:\\packs\\legacy.unitypackage",
                                {
                                  "FilePath": "C:\\packs\\modern.unitypackage",
                                  "FileName": {
                                    "invalid": true
                                  }
                                },
                                {
                                  "FilePath": {
                                    "invalid": true
                                  },
                                  "FileName": {
                                    "alsoInvalid": true
                                  }
                                },
                                null
                              ]
                            }
                            """;

        var settings = AppSettingsService.DeserializeForTests(json);

        Assert.Equal(2, settings.ExtractedUnitypackages.Count);
        Assert.Equal(@"C:\packs\legacy.unitypackage", settings.ExtractedUnitypackages[0].FilePath);
        Assert.Equal("legacy.unitypackage", settings.ExtractedUnitypackages[0].FileName);
        Assert.Equal(@"C:\packs\modern.unitypackage", settings.ExtractedUnitypackages[1].FilePath);
        Assert.Equal("modern.unitypackage", settings.ExtractedUnitypackages[1].FileName);
    }

    [Fact]
    public void DeserializeForTests_DeduplicatesByPathAndFallbacksInvalidDates()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        const string json = """
                            {
                              "ExtractedUnitypackages": [
                                {
                                  "FilePath": "C:\\packs\\same.unitypackage",
                                  "FileName": "Same",
                                  "DateExtracted": "not-a-date"
                                },
                                {
                                  "FilePath": "c:\\PACKS\\SAME.unitypackage",
                                  "FileName": "Duplicate",
                                  "DateExtracted": 1704067200
                                },
                                {
                                  "FileName": "Name Only",
                                  "DateExtracted": 1704067200000
                                }
                              ]
                            }
                            """;

        var settings = AppSettingsService.DeserializeForTests(json);

        Assert.Equal(2, settings.ExtractedUnitypackages.Count);
        Assert.Equal(@"C:\packs\same.unitypackage", settings.ExtractedUnitypackages[0].FilePath);
        Assert.InRange(settings.ExtractedUnitypackages[0].DateExtracted, before, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal("Name Only", settings.ExtractedUnitypackages[1].FileName);
        Assert.Equal(string.Empty, settings.ExtractedUnitypackages[1].FilePath);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1704067200000),
            settings.ExtractedUnitypackages[1].DateExtracted);
    }

    [Fact]
    public void DeserializeForTests_AcceptsUnixSecondsDate()
    {
        const string json = """
                            {
                              "ExtractedUnitypackages": [
                                {
                                  "FilePath": "C:\\packs\\seconds.unitypackage",
                                  "DateExtracted": 1704067200
                                }
                              ]
                            }
                            """;

        var settings = AppSettingsService.DeserializeForTests(json);

        Assert.Single(settings.ExtractedUnitypackages);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1704067200), settings.ExtractedUnitypackages[0].DateExtracted);
    }

    [Fact]
    public async Task Save_Retries_WhenSettingsFileTemporarilyLocked_AndEventuallySucceeds()
    {
      var initial = AppSettingsService.CreateDefault();
      initial.DefaultOutputPath = @"C:\initial";
      AppSettingsService.Save(initial);

      var lockStream = new FileStream(_settingsFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

      var releaseTask = Task.Run(async () =>
      {
        await Task.Delay(250);
        lockStream.Dispose();
      });

      var updated = AppSettingsService.CreateDefault();
      updated.DefaultOutputPath = @"C:\updated";

      AppSettingsService.Save(updated);
      await releaseTask;

      var loaded = AppSettingsService.Load();
      Assert.Equal(@"C:\updated", loaded.DefaultOutputPath);
      Assert.Null(AppSettingsService.LastError);
    }

    [Fact]
    public async Task Load_Retries_WhenSettingsFileTemporarilyLocked_AndEventuallySucceeds()
    {
      const string json = """
                          {
                            "DefaultOutputPath": "C:\\load-retry"
                          }
                          """;

      await File.WriteAllTextAsync(_settingsFilePath, json);

      var lockStream = new FileStream(_settingsFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

      var releaseTask = Task.Run(async () =>
      {
        await Task.Delay(250);
        lockStream.Dispose();
      });

      var loaded = AppSettingsService.Load();
      await releaseTask;

      Assert.Equal(@"C:\load-retry", loaded.DefaultOutputPath);
      Assert.Null(AppSettingsService.LastError);
    }

    [Fact]
    public void Save_UsesAtomicReplace_ProducesValidJson_AndNoTempLeak()
    {
      var staleTempPath = Path.Combine(_settingsDirectory, $"settings.json.{Guid.NewGuid():N}.tmp");
      File.WriteAllText(staleTempPath, "stale");
      File.SetLastWriteTimeUtc(staleTempPath, DateTime.UtcNow.AddMinutes(-10));

      var settings = AppSettingsService.CreateDefault();
      settings.DefaultOutputPath = @"C:\atomic";
      AppSettingsService.Save(settings);

      var content = File.ReadAllText(_settingsFilePath);
      var parsed = AppSettingsService.DeserializeForTests(content);

      Assert.Equal(@"C:\atomic", parsed.DefaultOutputPath);
      Assert.Empty(Directory.GetFiles(_settingsDirectory, "settings.json.*.tmp"));
    }
}