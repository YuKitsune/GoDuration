using Xunit;
using NodaTime;

namespace GoDuration.NodaTime.Tests;

public class GoDurationConverterTests
{
    // --- Parse: nanosecond → Duration (single-nanosecond precision) ---

    [Theory]
    [InlineData("1ns", 1L)] // full ns precision, unlike the TimeSpan variant
    [InlineData("100ns", 100L)]
    [InlineData("1s", 1_000_000_000L)]
    [InlineData("1h30m", 5_400_000_000_000L)]
    [InlineData("-1h30m", -5_400_000_000_000L)]
    [InlineData("0", 0L)]
    public void Parse_PreservesNanosecondPrecision(string input, long expectedNs)
    {
        var result = GoDurationConverter.Parse(input);
        Assert.Equal(expectedNs, result.ToInt64Nanoseconds());
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
        Assert.Equal(Duration.Zero, result);
    }

    [Fact]
    public void TryParse_SuccessYieldsParsedValue()
    {
        Assert.True(GoDurationConverter.TryParse("1h30m", out var result));
        Assert.Equal(Duration.FromMinutes(90), result);
    }

    // --- Format: Duration → nanoseconds via ToInt64Nanoseconds ---

    [Theory]
    [InlineData(1L, "1ns")] // NodaTime keeps single-ns precision
    [InlineData(1_500L, "1.5µs")]
    [InlineData(1_000_000_000L, "1s")]
    [InlineData(-1_000_000_000L, "-1s")]
    [InlineData(0L, "0s")]
    public void Format_ConvertsDurationToNanoseconds(long nanoseconds, string expected)
    {
        Assert.Equal(expected, GoDurationConverter.Format(Duration.FromNanoseconds(nanoseconds)));
    }

    [Fact]
    public void Format_ThrowsWhenValueExceedsGoRange()
    {
        // Well beyond ±292 years; NodaTime.Duration.ToInt64Nanoseconds throws.
        var beyondRange = Duration.FromDays(365 * 500);
        Assert.Throws<OverflowException>(() => GoDurationConverter.Format(beyondRange));
    }

    // --- Round-trip: Format then Parse recovers the same value ---

    [Theory]
    [InlineData("1ns")]
    [InlineData("500ns")]
    [InlineData("300ms")]
    [InlineData("1.5s")]
    [InlineData("500µs")]
    [InlineData("1h30m0s")]
    [InlineData("-1h30m0s")]
    [InlineData("0s")]
    public void FormatThenParse_RoundTrips(string formatted)
    {
        var parsed = GoDurationConverter.Parse(formatted);
        Assert.Equal(formatted, GoDurationConverter.Format(parsed));
    }
}
