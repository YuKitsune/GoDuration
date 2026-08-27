using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace GoDuration.YamlDotNet.Tests;

public class GoDurationTimeSpanYamlTypeConverterTests
{
    private static ISerializer BuildSerializer() =>
        new SerializerBuilder()
            .WithTypeConverter(new GoDurationTimeSpanYamlTypeConverter())
            .Build();

    private static IDeserializer BuildDeserializer() =>
        new DeserializerBuilder()
            .WithTypeConverter(new GoDurationTimeSpanYamlTypeConverter())
            .Build();

    [Theory]
    [InlineData("1s", 10_000_000L)]
    [InlineData("300ms", 3_000_000L)]
    [InlineData("1h30m", 54_000_000_000L)]
    [InlineData("-1.5h", -54_000_000_000L)]
    [InlineData("0", 0L)]
    public void Deserialize_ScalarToTimeSpan(string yamlScalar, long expectedTicks)
    {
        var value = BuildDeserializer().Deserialize<TimeSpan>(yamlScalar);
        Assert.Equal(expectedTicks, value.Ticks);
    }

    [Theory]
    [InlineData(10_000_000L, "1s")]
    [InlineData(54_000_000_000L, "1h30m0s")]
    [InlineData(0L, "0s")]
    public void Serialize_TimeSpanToScalar(long ticks, string expectedScalar)
    {
        var yaml = BuildSerializer().Serialize(TimeSpan.FromTicks(ticks)).TrimEnd('\r', '\n');
        Assert.Equal(expectedScalar, yaml);
    }

    [Fact]
    public void Deserialize_InvalidScalarThrows()
    {
        Assert.Throws<YamlException>(() => BuildDeserializer().Deserialize<TimeSpan>("garbage"));
    }

    private sealed class Config
    {
        public TimeSpan Timeout { get; set; }
    }

    [Fact]
    public void PocoRoundTrip()
    {
        var original = new Config { Timeout = TimeSpan.FromMinutes(90) };
        var yaml = BuildSerializer().Serialize(original);
        Assert.Contains("1h30m0s", yaml);

        var back = BuildDeserializer().Deserialize<Config>(yaml);
        Assert.Equal(original.Timeout, back.Timeout);
    }
}
