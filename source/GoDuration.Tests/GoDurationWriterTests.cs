using Xunit;

namespace GoDuration.Tests;

public class GoDurationWriterTests
{
    [Theory]
    [InlineData(0L, "0s")]
    [InlineData(1L, "1ns")]
    [InlineData(9L, "9ns")]
    [InlineData(100L, "100ns")]
    [InlineData(999L, "999ns")]
    [InlineData(1_000L, "1µs")]
    [InlineData(1_500L, "1.5µs")]
    [InlineData(1_000_000L, "1ms")]
    [InlineData(1_500_000L, "1.5ms")]
    [InlineData(300_000_000L, "300ms")]
    [InlineData(1_000_000_000L, "1s")]
    [InlineData(1_500_000_000L, "1.5s")]
    [InlineData(60_000_000_000L, "1m0s")]
    [InlineData(3_600_000_000_000L, "1h0m0s")]
    [InlineData(5_400_000_000_000L, "1h30m0s")]
    [InlineData(-5_400_000_000_000L, "-1h30m0s")]
    [InlineData(5_445_000_000_000L, "1h30m45s")]
    public void Write_MatchesGoStringOutput(long nanoseconds, string expected)
    {
        Assert.Equal(expected, GoDurationWriter.Write(nanoseconds));
    }

    // --- IncludePositiveSign ---

    [Theory]
    [InlineData(1_000_000_000L, "+1s")]
    [InlineData(-1_000_000_000L, "-1s")]
    [InlineData(0L, "0s")] // zero never gets a sign
    public void Write_IncludePositiveSign(long nanoseconds, string expected)
    {
        var options = new DurationFormatOptions { IncludePositiveSign = true };
        Assert.Equal(expected, GoDurationWriter.Write(nanoseconds, options));
    }

    // --- MicrosecondSymbol ---

    [Fact]
    public void Write_AsciiMicrosecondSymbol_UsesUs()
    {
        var options = new DurationFormatOptions { MicrosecondSymbol = MicrosecondSymbol.Ascii };
        Assert.Equal("1.5us", GoDurationWriter.Write(1_500L, options));
    }

    [Fact]
    public void Write_MuMicrosecondSymbol_IsDefault()
    {
        Assert.Equal("1.5µs", GoDurationWriter.Write(1_500L));
    }

    // --- OmitZeroUnits ---

    [Theory]
    [InlineData(3_600_000_000_000L, "1h")] // 1h0m0s
    [InlineData(5_400_000_000_000L, "1h30m")] // 1h30m0s
    [InlineData(3_600_000_000_000L + 30_000_000_000L, "1h30s")] // 1h0m30s
    [InlineData(1_800_000_000_000L, "30m")] // 30m0s
    [InlineData(30_000_000_000L, "30s")] // trailing seconds only
    [InlineData(0L, "0s")] // zero unchanged
    [InlineData(1_500_000_000L, "1.5s")] // non-zero fractional stays
    [InlineData(300_000_000L, "300ms")] // sub-second single-unit, unaffected
    public void Write_OmitZeroUnits(long nanoseconds, string expected)
    {
        var options = new DurationFormatOptions { OmitZeroUnits = true };
        Assert.Equal(expected, GoDurationWriter.Write(nanoseconds, options));
    }

    // --- Combined options ---

    [Fact]
    public void Write_PositiveSign_And_OmitZeroUnits()
    {
        var options = new DurationFormatOptions
        {
            IncludePositiveSign = true,
            OmitZeroUnits = true
        };
        Assert.Equal("+1h30s", GoDurationWriter.Write(3_600_000_000_000L + 30_000_000_000L, options));
    }

    [Fact]
    public void Write_NegativeValue_WithOmitZeroUnits()
    {
        var options = new DurationFormatOptions { OmitZeroUnits = true };
        Assert.Equal("-1h30s", GoDurationWriter.Write(-(3_600_000_000_000L + 30_000_000_000L), options));
    }

    // --- Round-trip: Write then Read recovers the same nanosecond count ---

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(1_500L)]
    [InlineData(300_000_000L)]
    [InlineData(1_500_000_000L)]
    [InlineData(3_600_000_000_000L)]
    [InlineData(5_445_000_000_000L)]
    [InlineData(-5_400_000_000_000L)]
    public void RoundTrip_DefaultOptions(long nanoseconds)
    {
        var written = GoDurationWriter.Write(nanoseconds);
        Assert.True(GoDurationReader.TryRead(written, out var readBack, out _));
        Assert.Equal(nanoseconds, readBack);
    }

    [Theory]
    [InlineData(3_600_000_000_000L)]
    [InlineData(5_400_000_000_000L)]
    [InlineData(3_600_000_000_000L + 30_000_000_000L)]
    [InlineData(1_500_000_000L)]
    [InlineData(0L)]
    public void RoundTrip_OmitZeroUnits(long nanoseconds)
    {
        var options = new DurationFormatOptions { OmitZeroUnits = true };
        var written = GoDurationWriter.Write(nanoseconds, options);
        Assert.True(GoDurationReader.TryRead(written, out var readBack, out _));
        Assert.Equal(nanoseconds, readBack);
    }

    [Theory]
    [InlineData(1_500L)]
    [InlineData(1_500_000L)]
    [InlineData(1_500_000_000L)]
    public void RoundTrip_AsciiMicrosecondSymbol(long nanoseconds)
    {
        var options = new DurationFormatOptions { MicrosecondSymbol = MicrosecondSymbol.Ascii };
        var written = GoDurationWriter.Write(nanoseconds, options);
        Assert.True(GoDurationReader.TryRead(written, out var readBack, out _));
        Assert.Equal(nanoseconds, readBack);
    }

    [Theory]
    [InlineData(1_500_000_000L)]
    [InlineData(-1_500_000_000L)]
    [InlineData(0L)]
    public void RoundTrip_IncludePositiveSign(long nanoseconds)
    {
        var options = new DurationFormatOptions { IncludePositiveSign = true };
        var written = GoDurationWriter.Write(nanoseconds, options);
        Assert.True(GoDurationReader.TryRead(written, out var readBack, out _));
        Assert.Equal(nanoseconds, readBack);
    }
}
