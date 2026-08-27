using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace GoDuration.SystemTextJson.Tests;

public class GoDurationTimeSpanJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new GoDurationTimeSpanJsonConverter() }
    };

    [Theory]
    [InlineData("\"1s\"", 10_000_000L)]
    [InlineData("\"300ms\"", 3_000_000L)]
    [InlineData("\"1h30m\"", 54_000_000_000L)]
    [InlineData("\"-1.5h\"", -54_000_000_000L)]
    [InlineData("\"0\"", 0L)]
    public void Deserialize_StringToTimeSpan(string json, long expectedTicks)
    {
        var value = JsonSerializer.Deserialize<TimeSpan>(json, Options);
        Assert.Equal(expectedTicks, value.Ticks);
    }

    [Theory]
    [InlineData(10_000_000L, "\"1s\"")]
    [InlineData(54_000_000_000L, "\"1h30m0s\"")]
    [InlineData(0L, "\"0s\"")]
    public void Serialize_TimeSpanToString(long ticks, string expectedJson)
    {
        var json = JsonSerializer.Serialize(TimeSpan.FromTicks(ticks), Options);
        Assert.Equal(expectedJson, json);
    }

    [Theory]
    [InlineData("123")] // number
    [InlineData("true")] // bool
    [InlineData("null")] // null
    [InlineData("{}")] // object
    [InlineData("[]")] // array
    public void Deserialize_RejectsNonString(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TimeSpan>(json, Options));
    }

    [Fact]
    public void Deserialize_InvalidStringThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TimeSpan>("\"garbage\"", Options));
    }

    private sealed class Config
    {
        [JsonPropertyName("timeout")]
        public TimeSpan Timeout { get; set; }
    }

    [Fact]
    public void PocoRoundTrip()
    {
        var original = new Config { Timeout = TimeSpan.FromMinutes(90) };
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Equal("{\"timeout\":\"1h30m0s\"}", json);

        var back = JsonSerializer.Deserialize<Config>(json, Options)!;
        Assert.Equal(original.Timeout, back.Timeout);
    }

    // --- Format options plumbed through the converter --------------------------

    [Fact]
    public void Serialize_HonoursOmitZeroUnits()
    {
        var options = new JsonSerializerOptions
        {
            Converters =
            {
                new GoDurationTimeSpanJsonConverter(new DurationFormatOptions { OmitZeroUnits = true })
            }
        };

        var json = JsonSerializer.Serialize(TimeSpan.FromHours(1), options);
        Assert.Equal("\"1h\"", json);
    }

    [Fact]
    public void Serialize_HonoursIncludePositiveSign()
    {
        // The default System.Text.Json encoder escapes the plus sign for HTML safety.
        // Use the relaxed encoder so the plus sign stays literal in the output.
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters =
            {
                new GoDurationTimeSpanJsonConverter(new DurationFormatOptions { IncludePositiveSign = true })
            }
        };

        var json = JsonSerializer.Serialize(TimeSpan.FromSeconds(1), options);
        Assert.Equal("\"+1s\"", json);
    }
}
