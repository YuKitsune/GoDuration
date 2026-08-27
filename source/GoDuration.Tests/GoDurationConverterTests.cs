using Xunit;

namespace GoDuration.Tests;

public class GoDurationConverterTests
{
    // --- Parse: rounds nanoseconds to the nearest 100-ns tick ---

    [Theory]
    [InlineData("1ns", 0L)] // sub-tick → 0 ticks
    [InlineData("49ns", 0L)] // rounds down to 0
    [InlineData("50ns", 1L)] // rounds half-away-from-zero → 1 tick
    [InlineData("100ns", 1L)] // exact
    [InlineData("150ns", 2L)] // rounds up
    [InlineData("-1ns", 0L)]
    [InlineData("-50ns", -1L)]
    [InlineData("-100ns", -1L)]
    [InlineData("1h", 36_000_000_000L)]
    public void Parse_RoundsNanosecondsToNearestTick(string input, long expectedTicks)
    {
        Assert.Equal(expectedTicks, GoDurationConverter.Parse(input).Ticks);
    }

    // --- Parse: wrapper glue ---

    [Fact]
    public void Parse_NullThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GoDurationConverter.Parse(null!));
    }

    [Fact]
    public void Parse_InvalidThrowsFormat()
    {
        Assert.Throws<FormatException>(() => GoDurationConverter.Parse("nope"));
    }

    // --- TryParse: wrapper glue ---

    [Theory]
    [InlineData("1s", true)]
    [InlineData("garbage", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryParse_ReportsSuccess(string? input, bool expected)
    {
        Assert.Equal(expected, GoDurationConverter.TryParse(input, out _));
    }

    [Fact]
    public void TryParse_FailureYieldsZero()
    {
        GoDurationConverter.TryParse("nope", out var result);
        Assert.Equal(TimeSpan.Zero, result);
    }

    // --- Format: TimeSpan.Ticks × 100 → nanoseconds ---

    [Theory]
    [InlineData(0L, "0s")]
    [InlineData(1L, "100ns")] // 1 tick = 100 ns
    [InlineData(15L, "1.5µs")]
    [InlineData(10_000_000L, "1s")]
    [InlineData(-10_000_000L, "-1s")]
    public void Format_ConvertsTicksToNanoseconds(long ticks, string expected)
    {
        Assert.Equal(expected, GoDurationConverter.Format(TimeSpan.FromTicks(ticks)));
    }

    [Fact]
    public void Format_ThrowsWhenValueExceedsGoRange()
    {
        // TimeSpan.MaxValue.Ticks × 100 overflows int64 nanoseconds.
        Assert.Throws<OverflowException>(() => GoDurationConverter.Format(TimeSpan.MaxValue));
    }

    // --- Round-trip: Format then Parse recovers the same TimeSpan ---

    [Theory]
    [InlineData("300ms")]
    [InlineData("1.5s")]
    [InlineData("500µs")]
    [InlineData("1h30m0s")]
    [InlineData("-1h30m0s")]
    [InlineData("100ns")]
    [InlineData("0s")]
    public void FormatThenParse_RoundTrips(string formatted)
    {
        var parsed = GoDurationConverter.Parse(formatted);
        Assert.Equal(formatted, GoDurationConverter.Format(parsed));
    }
}
