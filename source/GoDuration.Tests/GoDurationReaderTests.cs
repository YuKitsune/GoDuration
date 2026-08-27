using Xunit;

namespace GoDuration.Tests;

public class GoDurationReaderTests
{
    [Theory]
    [InlineData("0", 0L)]
    [InlineData("+0", 0L)]
    [InlineData("-0", 0L)]
    [InlineData("0s", 0L)]
    [InlineData("1ns", 1L)]
    [InlineData("100ns", 100L)]
    [InlineData("1us", 1_000L)]
    [InlineData("1µs", 1_000L)] // U+00B5
    [InlineData("1μs", 1_000L)] // U+03BC
    [InlineData("1ms", 1_000_000L)]
    [InlineData("300ms", 300_000_000L)]
    [InlineData("1s", 1_000_000_000L)]
    [InlineData("1m", 60_000_000_000L)]
    [InlineData("1h", 3_600_000_000_000L)]
    [InlineData("1.5h", 5_400_000_000_000L)]
    [InlineData("-1.5h", -5_400_000_000_000L)]
    [InlineData("+1.5h", 5_400_000_000_000L)]
    [InlineData(".5s", 500_000_000L)]
    [InlineData("1.s", 1_000_000_000L)] // "1." is a valid number in Go
    [InlineData("2h45m", 9_900_000_000_000L)]
    [InlineData("1h30m45s", 5_445_000_000_000L)]
    [InlineData("30s1m", 90_000_000_000L)] // order-independent (30s + 1m)
    public void TryRead_ReturnsExpectedNanoseconds(string input, long expected)
    {
        Assert.True(GoDurationReader.TryRead(input, out var nanoseconds, out var error));
        Assert.Equal(expected, nanoseconds);
        Assert.Null(error);
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
    [InlineData("1")]
    [InlineData("1x")]
    [InlineData("1.5.5s")]
    [InlineData("1s ")]
    [InlineData(" 1s")]
    [InlineData("1s1")]
    [InlineData("1 s")]
    [InlineData("-.s")]
    public void TryRead_ReturnsFalseOnInvalid(string input)
    {
        Assert.False(GoDurationReader.TryRead(input, out var nanoseconds, out var error));
        Assert.Equal(0L, nanoseconds);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("", "invalid duration \"\"")]
    [InlineData("-", "invalid duration \"-\"")]
    [InlineData("+", "invalid duration \"+\"")]
    [InlineData(" 1s", "invalid duration \" 1s\"")]
    [InlineData(".", "invalid duration \".\"")]
    [InlineData(".s", "invalid duration \".s\"")]
    [InlineData("-.s", "invalid duration \"-.s\"")]
    [InlineData("1", "missing unit in duration \"1\"")]
    [InlineData("1s1", "missing unit in duration \"1s1\"")]
    [InlineData("1.5.5s", "missing unit in duration \"1.5.5s\"")]
    [InlineData("1x", "unknown unit \"x\" in duration \"1x\"")]
    [InlineData("1 s", "unknown unit \" s\" in duration \"1 s\"")]
    [InlineData("1s ", "unknown unit \"s \" in duration \"1s \"")]
    [InlineData("24h 30m", "unknown unit \"h \" in duration \"24h 30m\"")]
    public void TryRead_ProducesDescriptiveErrorMessage(string input, string expectedMessage)
    {
        Assert.False(GoDurationReader.TryRead(input, out _, out var error));
        Assert.Equal(expectedMessage, error);
    }

    [Fact]
    public void TryRead_NullThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GoDurationReader.TryRead(null!, out _, out _));
    }

    [Fact]
    public void TryRead_ValueBeyondGoRangeReturnsFalse()
    {
        // 10 million hours ~= 1141 years; total nanoseconds exceeds int64.
        Assert.False(GoDurationReader.TryRead("10000000h", out var nanoseconds, out var error));
        Assert.Equal(0L, nanoseconds);
        Assert.Equal("invalid duration \"10000000h\"", error);
    }
}
