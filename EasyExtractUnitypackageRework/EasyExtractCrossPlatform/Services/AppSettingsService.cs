using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyExtractCrossPlatform.Services;

public static class AppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new HistoryEntryListJsonConverter(),
            new SafeWindowStateConverter(),
            new ExtractedPackageModelConverter(),
            new SafeDateTimeOffsetConverter(),
            new JsonStringEnumConverter()
        }
    };

    public static string SettingsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract");

    public static string SettingsFilePath { get; } = Path.Combine(SettingsDirectory, "settings.json");

    public static Exception? LastError { get; private set; }

    public static AppSettings Load()
    {
        LastError = null;
        var stopwatch = Stopwatch.StartNew();
        AppSettings? resolvedSettings = null;
        var source = "unknown";
        Exception? failure = null;

        LoggingService.LogInformation($"Loading application settings from '{SettingsFilePath}'.");

        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                LoggingService.LogInformation("Settings file not found. Creating default configuration.");
                var defaults = CreateDefault();
                Save(defaults);
                LoggingService.LogInformation("Default settings persisted successfully.");
                resolvedSettings = defaults;
                source = "defaults";
            }
            else
            {
                using var stream = File.OpenRead(SettingsFilePath);
                var settings = DeserializeSettings(stream);
                LoggingService.LogInformation("Settings loaded successfully.");
                resolvedSettings = settings;
                source = "existing";
            }
        }
        catch (Exception ex)
        {
            LastError = ex;
            LoggingService.LogError("Failed to load settings. Falling back to defaults.", ex);
            resolvedSettings = CreateDefault();
            source = "fallback";
            failure = ex;
        }
        finally
        {
            stopwatch.Stop();
            LoggingService.LogPerformance("AppSettingsService.Load", stopwatch.Elapsed,
                details: $"source={source}|status={(failure is null ? "ok" : "failed")}");
            LoggingService.LogMemoryUsage("AppSettingsService.Load");

            if (resolvedSettings is not null)
            {
                resolvedSettings.ExtractionLimits =
                    UnityPackageExtractionLimits.Normalize(resolvedSettings.ExtractionLimits);
                LoggingService.ApplySettingsSnapshot(resolvedSettings, "load");
            }
        }

        return resolvedSettings!;
    }

    public static void Save(AppSettings settings)
    {
        settings.AppTitle = AppSettings.DefaultAppTitle;
        settings.ExtractionLimits = UnityPackageExtractionLimits.Normalize(settings.ExtractionLimits);
        NormalizeExtractedPackageModels(settings);
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        LoggingService.ApplySettingsSnapshot(settings, "save");
        try
        {
            LoggingService.LogInformation($"Saving application settings to '{SettingsFilePath}'.");
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(SettingsFilePath, json);
            LoggingService.LogInformation("Settings saved successfully.");
            success = true;
        }
        catch (Exception ex)
        {
            var diskFull = DiskSpaceHelper.IsDiskFull(ex);
            var message = diskFull
                ? $"{DiskSpaceHelper.BuildFriendlyMessage(SettingsFilePath)} Unable to save application settings."
                : "Failed to save application settings.";

            var logMessage = diskFull
                ? $"{message} | path='{SettingsFilePath}'"
                : message;

            LoggingService.LogError(logMessage, ex);

            if (diskFull)
                throw new IOException(message, ex);

            throw;
        }
        finally
        {
            stopwatch.Stop();
            LoggingService.LogPerformance("AppSettingsService.Save", stopwatch.Elapsed,
                details: $"status={(success ? "ok" : "failed")}");
            LoggingService.LogMemoryUsage("AppSettingsService.Save");
        }
    }

    public static AppSettings CreateDefault()
    {
        Directory.CreateDirectory(SettingsDirectory);
        LoggingService.LogInformation("Creating default application settings profile.");

        var defaults = new AppSettings
        {
            DefaultOutputPath = Path.Combine(SettingsDirectory, "Extracted"),
            DefaultTempPath = Path.Combine(SettingsDirectory, "Temp"),
            ApplicationTheme = 0,
            ContextMenuToggle = true,
            DiscordRpc = true,
            ExtractedCategoryStructure = false,
            EnableSecurityScanning = false,
            EnableSound = true,
            EnableNotifications = true,
            SoundVolume = 1.0,
            CustomBackgroundImage = new CustomBackgroundImageSettings
            {
                IsEnabled = false
            },
            ExtractionLimits = UnityPackageExtractionLimits.Default,
            FirstRun = false,
            LastExtractionTime = DateTimeOffset.Now,
            WindowPlacements = new Dictionary<string, WindowPlacementSettings>(StringComparer.OrdinalIgnoreCase)
        };

        UpdateStoredVersion(defaults);
        EnsureWindowPlacementsStorage(defaults);
        LoggingService.LogInformation(
            $"Default settings created with output '{defaults.DefaultOutputPath}' and temp '{defaults.DefaultTempPath}'.");
        return defaults;
    }

    internal static AppSettings DeserializeForTests(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "{}"));
        return DeserializeSettings(stream);
    }

    private static void UpdateStoredVersion(AppSettings settings)
    {
        var version = VersionProvider.GetApplicationVersion();
        if (string.IsNullOrWhiteSpace(version))
            return;

        if (settings.Update is null)
            settings.Update = new UpdateSettings();

        settings.Update.CurrentVersion = version;
        LoggingService.LogInformation($"Recorded application version '{version}' in settings.");
    }

    private static void EnsureWindowPlacementsStorage(AppSettings settings)
    {
        if (settings is null)
            return;

        if (settings.WindowPlacements is null)
        {
            settings.WindowPlacements =
                new Dictionary<string, WindowPlacementSettings>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        if (settings.WindowPlacements.Comparer == StringComparer.OrdinalIgnoreCase)
            return;

        settings.WindowPlacements = new Dictionary<string, WindowPlacementSettings>(settings.WindowPlacements,
            StringComparer.OrdinalIgnoreCase);
    }

    private static AppSettings DeserializeSettings(Stream stream)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(stream, SerializerOptions) ?? CreateDefault();
        settings.AppTitle = AppSettings.DefaultAppTitle;
        UpdateStoredVersion(settings);
        EnsureWindowPlacementsStorage(settings);
        NormalizeExtractedPackageModels(settings);
        settings.ExtractionLimits = UnityPackageExtractionLimits.Normalize(settings.ExtractionLimits);
        return settings;
    }

    private static void NormalizeExtractedPackageModels(AppSettings settings)
    {
        if (settings.ExtractedUnitypackages is null || settings.ExtractedUnitypackages.Count == 0)
        {
            settings.ExtractedUnitypackages = new List<ExtractedPackageModel>();
            return;
        }

        var normalized = new List<ExtractedPackageModel>(settings.ExtractedUnitypackages.Count);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var existing in settings.ExtractedUnitypackages)
        {
            if (existing is null)
                continue;

            var filePath = existing.FilePath?.Trim() ?? string.Empty;
            var fileName = existing.FileName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(filePath))
                fileName = GetFileNameSafe(filePath);

            if (string.IsNullOrWhiteSpace(filePath) && string.IsNullOrWhiteSpace(fileName))
                continue;

            if (!string.IsNullOrWhiteSpace(filePath) && !seenPaths.Add(filePath))
                continue;

            var dateExtracted = existing.DateExtracted == default
                ? DateTimeOffset.UtcNow
                : existing.DateExtracted;

            normalized.Add(new ExtractedPackageModel
            {
                FilePath = filePath,
                FileName = fileName ?? string.Empty,
                DateExtracted = dateExtracted
            });
        }

        settings.ExtractedUnitypackages = normalized;
    }

    private static string GetFileNameSafe(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFileName(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    public class SafeWindowStateConverter : JsonConverter<WindowState>
    {
        public override WindowState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt32(out var intValue) && Enum.IsDefined(typeof(WindowState), intValue))
                    return (WindowState)intValue;
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (Enum.TryParse<WindowState>(value, true, out var result)) return result;
            }

            reader.Skip();
            return WindowState.Normal;
        }

        public override void Write(Utf8JsonWriter writer, WindowState value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    private class ExtractedPackageModelConverter : JsonConverter<ExtractedPackageModel>
    {
        public override ExtractedPackageModel? Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var path = reader.GetString();
                if (string.IsNullOrWhiteSpace(path))
                    return null;

                return new ExtractedPackageModel
                {
                    FilePath = path,
                    FileName = Path.GetFileName(path),
                    DateExtracted = DateTimeOffset.UtcNow
                };
            }

            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return null;
            }

            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;

            var filePath = root.TryGetProperty(nameof(ExtractedPackageModel.FilePath),
                out var filePathElement)
                ? TryReadString(filePathElement)
                : null;

            var fileName = root.TryGetProperty(nameof(ExtractedPackageModel.FileName),
                out var fileNameElement)
                ? TryReadString(fileNameElement)
                : null;

            if (string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(filePath))
                fileName = GetFileNameSafe(filePath);

            var date = DateTimeOffset.UtcNow;
            if (root.TryGetProperty(nameof(ExtractedPackageModel.DateExtracted),
                    out var dateElement))
            {
                var parsedDate = TryReadDate(dateElement);
                if (parsedDate.HasValue)
                    date = parsedDate.Value;
            }

            if (string.IsNullOrWhiteSpace(fileName) && string.IsNullOrWhiteSpace(filePath))
                return null;

            return new ExtractedPackageModel
            {
                FileName = fileName ?? string.Empty,
                FilePath = filePath ?? string.Empty,
                DateExtracted = date
            };
        }

        public override void Write(Utf8JsonWriter writer, ExtractedPackageModel value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(ExtractedPackageModel.FileName), value.FileName);
            writer.WriteString(nameof(ExtractedPackageModel.FilePath), value.FilePath);
            writer.WriteString(nameof(ExtractedPackageModel.DateExtracted), value.DateExtracted);
            writer.WriteEndObject();
        }

        private static string? TryReadString(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => element.GetRawText(),
                JsonValueKind.False => element.GetRawText(),
                _ => null
            };
        }

        private static DateTimeOffset? TryReadDate(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    return parsed;

                return null;
            }

            if (element.ValueKind != JsonValueKind.Number)
                return null;

            if (!TryReadUnixTimestamp(element, out var unixTimestamp))
                return null;

            try
            {
                var absoluteValue = Math.Abs(unixTimestamp);
                return absoluteValue > 9_999_999_999
                    ? DateTimeOffset.FromUnixTimeMilliseconds(unixTimestamp)
                    : DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryReadUnixTimestamp(JsonElement element, out long value)
        {
            if (element.TryGetInt64(out value))
                return true;

            if (!element.TryGetDouble(out var number) || double.IsNaN(number) || double.IsInfinity(number))
            {
                value = 0;
                return false;
            }

            value = Convert.ToInt64(Math.Round(number, MidpointRounding.AwayFromZero));
            return true;
        }
    }

    private class SafeDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
    {
        public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var dateString = reader.GetString();
                if (DateTimeOffset.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None,
                        out var date))
                    return date;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value);
            else
                writer.WriteNullValue();
        }
    }
}