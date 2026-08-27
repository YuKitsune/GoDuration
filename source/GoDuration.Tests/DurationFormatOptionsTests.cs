using Xunit;

namespace GoDuration.Tests;

public class DurationFormatOptionsTests
{
    // --- IncludePositiveSign ---------------------------------------------------

    [Theory]
    [InlineData(10_000_000L, "+1s")]
    [InlineData(-10_000_000L, "-1s")]
    [InlineData(0L, "0s")] // zero never gets a sign
    public void Format_IncludePositiveSign(long ticks, string expected)
    {
        var options = new DurationFormatOptions { IncludePositiveSign = true };
        Assert.Equal(expected, Duration.Format(TimeSpan.FromTicks(ticks), options));
    }

    // --- MicrosecondSymbol -----------------------------------------------------

    [Fact]
    public void Format_AsciiMicrosecondSymbol_UsesUs()
    {
        var options = new DurationFormatOptions { MicrosecondSymbol = MicrosecondSymbol.Ascii };
        Assert.Equal("1.5us", Duration.Format(TimeSpan.FromTicks(15), options));
    }

    [Fact]
    public void Format_MuMicrosecondSymbol_IsDefault()
    {
        Assert.Equal("1.5µs", Duration.Format(TimeSpan.FromTicks(15)));
    }

    // --- OmitZeroUnits ---------------------------------------------------------

    [Theory]
    [InlineData(36_000_000_000L, "1h")]                             // 1h0m0s
    [InlineData(54_000_000_000L, "1h30m")]                          // 1h30m0s
    [InlineData(36_000_000_000L + 300_000_000L, "1h30s")]           // 1h0m30s
    [InlineData(18_000_000_000L, "30m")]                            // 30m0s
    [InlineData(300_000_000L, "30s")]                               // trailing seconds only
    [InlineData(0L, "0s")]                                          // zero unchanged
    [InlineData(15_000_000L, "1.5s")]                               // non-zero fractional stays
    [InlineData(3_000_000L, "300ms")]                               // sub-second single-unit, unaffected
    public void Format_OmitZeroUnits(long ticks, string expected)
    {
        var options = new DurationFormatOptions { OmitZeroUnits = true };
        Assert.Equal(expected, Duration.Format(TimeSpan.FromTicks(ticks), options));
    }

    // --- Combined options ------------------------------------------------------

    [Fact]
    public void Format_PositiveSign_And_OmitZeroUnits()
    {
        var options = new DurationFormatOptions
        {
            IncludePositiveSign = true,
            OmitZeroUnits = true
        };
        Assert.Equal("+1h30s", Duration.Format(TimeSpan.FromTicks(36_000_000_000L + 300_000_000L), options));
    }

    [Fact]
    public void Format_NegativeValue_WithOmitZeroUnits()
    {
        var options = new DurationFormatOptions { OmitZeroUnits = true };
        Assert.Equal("-1h30s", Duration.Format(TimeSpan.FromTicks(-(36_000_000_000L + 300_000_000L)), options));
    }

    // --- Round-trip: Format then Parse recovers the same TimeSpan -------------

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]                                                // 100ns
    [InlineData(15L)]                                               // 1.5µs
    [InlineData(3_000_000L)]                                        // 300ms
    [InlineData(15_000_000L)]                                       // 1.5s
    [InlineData(36_000_000_000L)]                                   // 1h
    [InlineData(54_450_000_000L)]                                   // 1h30m45s
    [InlineData(-54_000_000_000L)]                                  // -1h30m0s
    public void RoundTrip_DefaultOptions(long ticks)
    {
        var value = TimeSpan.FromTicks(ticks);
        Assert.Equal(value, Duration.Parse(Duration.Format(value)));
    }

    [Theory]
    [InlineData(36_000_000_000L)]                                   // 1h0m0s → 1h
    [InlineData(54_000_000_000L)]                                   // 1h30m0s → 1h30m
    [InlineData(36_000_000_000L + 300_000_000L)]                    // 1h0m30s → 1h30s
    [InlineData(15_000_000L)]                                       // 1.5s
    [InlineData(0L)]                                                // 0s
    public void RoundTrip_OmitZeroUnits(long ticks)
    {
        var options = new DurationFormatOptions { OmitZeroUnits = true };
        var value = TimeSpan.FromTicks(ticks);
        Assert.Equal(value, Duration.Parse(Duration.Format(value, options)));
    }

    [Theory]
    [InlineData(15L)]                                               // 1.5µs — parses via "us"
    [InlineData(15_000L)]                                           // 1.5ms
    [InlineData(15_000_000L)]                                       // 1.5s
    public void RoundTrip_AsciiMicrosecondSymbol(long ticks)
    {
        var options = new DurationFormatOptions { MicrosecondSymbol = MicrosecondSymbol.Ascii };
        var value = TimeSpan.FromTicks(ticks);
        Assert.Equal(value, Duration.Parse(Duration.Format(value, options)));
    }

    [Theory]
    [InlineData(15_000_000L)]                                       // +1.5s
    [InlineData(-15_000_000L)]                                      // -1.5s
    [InlineData(0L)]                                                // 0s (sign suppressed)
    public void RoundTrip_IncludePositiveSign(long ticks)
    {
        var options = new DurationFormatOptions { IncludePositiveSign = true };
        var value = TimeSpan.FromTicks(ticks);
        Assert.Equal(value, Duration.Parse(Duration.Format(value, options)));
    }
}
