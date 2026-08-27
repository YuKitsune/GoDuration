using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoDuration.SystemTextJson;

/// <summary>
/// System.Text.Json converter that reads and writes <see cref="TimeSpan"/> values
/// as Go-style duration strings. Only JSON string tokens are accepted on read.
/// </summary>
public sealed class GoDurationTimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected string for Go duration, got {reader.TokenType}.");

        var text = reader.GetString();
        if (text is null)
            throw new JsonException("Expected non-null string for Go duration.");

        try
        {
            return Duration.Parse(text);
        }
        catch (FormatException ex)
        {
            throw new JsonException(ex.Message, ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(Duration.Format(value));
    }
}
