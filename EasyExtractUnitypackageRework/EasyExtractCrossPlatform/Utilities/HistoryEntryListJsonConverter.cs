using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyExtractCrossPlatform.Utilities;

public sealed class HistoryEntryListJsonConverter : JsonConverter<List<HistoryEntry>>
{
    public override List<HistoryEntry> Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        var entries = new List<HistoryEntry>();
        if (reader.TokenType == JsonTokenType.Null)
            return entries;

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            if (TryReadSingleEntry(ref reader, options, out var single) && single is not null)
                entries.Add(single);
            return entries;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            if (TryReadSingleEntry(ref reader, options, out var entry) && entry is not null)
                entries.Add(entry);
        }

        return entries;
    }

    public override void Write(Utf8JsonWriter writer, List<HistoryEntry> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        if (value is not null)
            foreach (var entry in value)
            {
                if (entry is null)
                    continue;

                JsonSerializer.Serialize(writer, entry, options);
            }

        writer.WriteEndArray();
    }

    private static bool TryReadSingleEntry(
        ref Utf8JsonReader reader,
        JsonSerializerOptions options,
        out HistoryEntry? entry)
    {
        entry = reader.TokenType switch
        {
            JsonTokenType.StartObject => JsonSerializer.Deserialize<HistoryEntry>(ref reader, options),
            JsonTokenType.String => HistoryEntry.FromLegacyPath(reader.GetString(), DateTimeOffset.UtcNow),
            _ => null
        };

        if (entry is null)
        {
            reader.Skip();
            return false;
        }

        return true;
    }
}