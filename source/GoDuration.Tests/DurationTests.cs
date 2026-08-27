using Xunit;

namespace GoDuration.Tests;

public class DurationTests
{
    [Theory]
    [InlineData("0", 0L)]
    [InlineData("+0", 0L)]
    [InlineData("-0", 0L)]
    [InlineData("0s", 0L)]
    [InlineData("1ns", 0L)] // sub-tick, rounds to zero
    [InlineData("100ns", 1L)] // 100 ns == 1 tick
    [InlineData("1us", 10L)]
    [InlineData("1µs", 10L)] // U+00B5
    [InlineData("1μs", 10L)] // U+03BC
    [InlineData("1ms", 10_000L)]
    [InlineData("300ms", 3_000_000L)]
    [InlineData("1s", 10_000_000L)]
    [InlineData("1m", 600_000_000L)]
    [InlineData("1h", 36_000_000_000L)]
    [InlineData("1.5h", 54_000_000_000L)]
    [InlineData("-1.5h", -54_000_000_000L)]
    [InlineData("+1.5h", 54_000_000_000L)]
    [InlineData(".5s", 5_000_000L)]
    [InlineData("1.s", 10_000_000L)] // "1." is a valid number in Go
    [InlineData("2h45m", 99_000_000_000L)]
    [InlineData("1h30m45s", 54_450_000_000L)]
    [InlineData("30s1m", 36_000_000_000L + 300_000_000L)] // order-independent
    public void Parse_ReturnsExpectedTicks(string input, long expectedTicks)
    {
        var result = Duration.Parse(input);
        Assert.Equal(expectedTicks, result.Ticks);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]
    [InlineData("+")]
    [InlineData(".")]
    [InlineData(".s")]
    [InlineData("s")]
    [InlineData("h")]
    [InlineData("1")] // no unit and not bare "0"
    [InlineData("1x")]
    [InlineData("1.5.5s")]
    [InlineData("1s ")] // trailing garbage
    [InlineData(" 1s")] // leading whitespace
    [InlineData("1s1")] // dangling number
    [InlineData("1 s")] // space between number and unit
    [InlineData("-.s")]
    public void Parse_ThrowsOnInvalid(string input)
    {
        Assert.Throws<FormatException>(() => Duration.Parse(input));
    }

    [Fact]
    public void Parse_NullThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Duration.Parse(null!));
    }

    [Theory]
    [InlineData("1s", true)]
    [InlineData("garbage", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryParse_ReportsSuccess(string? input, bool expected)
    {
        var ok = Duration.TryParse(input, out _);
        Assert.Equal(expected, ok);
    }

    [Fact]
    public void TryParse_FailureYieldsDefault()
    {
        Duration.TryParse("nope", out var result);
        Assert.Equal(TimeSpan.Zero, result);
    }
}
