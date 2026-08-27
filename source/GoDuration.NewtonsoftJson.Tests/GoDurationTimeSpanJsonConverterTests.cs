using Newtonsoft.Json;
using Xunit;

namespace GoDuration.NewtonsoftJson.Tests;

public class GoDurationTimeSpanJsonConverterTests
{
    private static readonly JsonSerializerSettings Settings = new()
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
        var value = JsonConvert.DeserializeObject<TimeSpan>(json, Settings);
        Assert.Equal(expectedTicks, value.Ticks);
    }

    [Theory]
    [InlineData(10_000_000L, "\"1s\"")]
    [InlineData(54_000_000_000L, "\"1h30m0s\"")]
    [InlineData(0L, "\"0s\"")]
    public void Serialize_TimeSpanToString(long ticks, string expectedJson)
    {
        var json = JsonConvert.SerializeObject(TimeSpan.FromTicks(ticks), Settings);
        Assert.Equal(expectedJson, json);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Deserialize_RejectsNonString(string json)
    {
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<TimeSpan>(json, Settings));
    }

    [Fact]
    public void Deserialize_InvalidStringThrows()
    {
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<TimeSpan>("\"garbage\"", Settings));
    }

    private sealed class Config
    {
        [JsonProperty("timeout")]
        public TimeSpan Timeout { get; set; }
    }

    [Fact]
    public void PocoRoundTrip()
    {
        var original = new Config { Timeout = TimeSpan.FromMinutes(90) };
        var json = JsonConvert.SerializeObject(original, Settings);
        Assert.Equal("{\"timeout\":\"1h30m0s\"}", json);

        var back = JsonConvert.DeserializeObject<Config>(json, Settings)!;
        Assert.Equal(original.Timeout, back.Timeout);
    }

    // --- Format options plumbed through the converter --------------------------

    [Fact]
    public void Serialize_HonoursOmitZeroUnits()
    {
        var settings = new JsonSerializerSettings
        {
            Converters =
            {
                new GoDurationTimeSpanJsonConverter(new DurationFormatOptions { OmitZeroUnits = true })
            }
        };

        var json = JsonConvert.SerializeObject(TimeSpan.FromHours(1), settings);
        Assert.Equal("\"1h\"", json);
    }

    [Fact]
    public void Serialize_HonoursIncludePositiveSign()
    {
        var settings = new JsonSerializerSettings
        {
            Converters =
            {
                new GoDurationTimeSpanJsonConverter(new DurationFormatOptions { IncludePositiveSign = true })
            }
        };

        var json = JsonConvert.SerializeObject(TimeSpan.FromSeconds(1), settings);
        Assert.Equal("\"+1s\"", json);
    }
}
