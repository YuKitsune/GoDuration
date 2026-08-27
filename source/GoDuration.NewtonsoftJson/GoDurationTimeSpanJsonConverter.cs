using Newtonsoft.Json;

namespace GoDuration.NewtonsoftJson;

/// <summary>
/// Newtonsoft.Json converter that reads and writes <see cref="TimeSpan"/> values
/// as Go-style duration strings. Only JSON string tokens are accepted on read.
/// </summary>
public sealed class GoDurationTimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan ReadJson(
        JsonReader reader,
        Type objectType,
        TimeSpan existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException($"Expected string for Go duration, got {reader.TokenType}.");

        if (reader.Value is not string text)
            throw new JsonSerializationException("Expected non-null string for Go duration.");

        try
        {
            return Duration.Parse(text);
        }
        catch (FormatException ex)
        {
            throw new JsonSerializationException(ex.Message, ex);
        }
    }

    public override void WriteJson(JsonWriter writer, TimeSpan value, JsonSerializer serializer)
    {
        writer.WriteValue(Duration.Format(value));
    }
}
