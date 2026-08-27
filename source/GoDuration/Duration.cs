using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace GoDuration;

/// <summary>
/// Parses Go-style duration strings (e.g. "300ms", "-1.5h", "2h45m") into <see cref="TimeSpan"/>.
/// Follows the grammar used by Go's <c>time.ParseDuration</c>.
/// </summary>
public static class Duration
{
    public static TimeSpan Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!TryParse(input, out var result))
            throw new FormatException($"time: invalid duration \"{input}\"");

        return result;
    }
    
    public static bool TryParse(
        [NotNullWhen(true)] string? input,
        out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (string.IsNullOrEmpty(input))
            return false;

        return TryParseSpan(input.AsSpan(), out result);
    }

    private static bool TryParseSpan(ReadOnlySpan<char> input, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        var i = 0;
        var sign = 1;
        if (input[0] == '+') { i = 1; }
        else if (input[0] == '-') { sign = -1; i = 1; }

        if (i == input.Length)
            return false;

        // Go accepts a bare "0" (with no unit) as zero.
        if (input.Length - i == 1 && input[i] == '0')
            return true;

        var totalTicks = 0d;
        var anySegment = false;
        while (i < input.Length)
        {
            if (!TryReadNumber(input, ref i, out var number))
                return false;
            if (!TryReadUnit(input, ref i, out var unitTicks))
                return false;
            totalTicks += number * unitTicks;
            anySegment = true;
        }

        if (!anySegment)
            return false;

        try
        {
            result = TimeSpan.FromTicks(checked((long)Math.Round(sign * totalTicks)));
            return true;
        }
        catch (OverflowException) { return false; }
    }

    private static bool TryReadNumber(ReadOnlySpan<char> input, ref int i, out double value)
    {
        value = 0d;
        var start = i;

        while (i < input.Length && (uint)(input[i] - '0') <= 9)
            i++;
        var hasIntegerDigits = i > start;

        var hasDot = false;
        var fractionStart = i;
        if (i < input.Length && input[i] == '.')
        {
            hasDot = true;
            i++;
            fractionStart = i;
            while (i < input.Length && (uint)(input[i] - '0') <= 9)
                i++;
        }
        var hasFractionDigits = hasDot && i > fractionStart;

        if (!hasIntegerDigits && !hasFractionDigits)
            return false;

        return double.TryParse(input[start..i], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUnit(ReadOnlySpan<char> input, ref int i, out double ticksPerUnit)
    {
        ticksPerUnit = 0d;
        var remaining = input.Length - i;
        if (remaining <= 0)
            return false;

        if (remaining >= 2)
        {
            var c0 = input[i];
            var c1 = input[i + 1];
            if (c1 == 's')
            {
                switch (c0)
                {
                    case 'n': ticksPerUnit = 0.01d; i += 2; return true;
                    case 'u': ticksPerUnit = 10d; i += 2; return true;
                    case 'µ': ticksPerUnit = 10d; i += 2; return true; // µs
                    case 'μ': ticksPerUnit = 10d; i += 2; return true; // μs
                    case 'm': ticksPerUnit = 10_000d; i += 2; return true;
                }
            }
        }

        switch (input[i])
        {
            case 's': ticksPerUnit = 10_000_000d; i++; return true;
            case 'm': ticksPerUnit = 600_000_000d; i++; return true;
            case 'h': ticksPerUnit = 36_000_000_000d; i++; return true;
            default: return false;
        }
    }

    /// <summary>
    /// Renders <paramref name="value"/> as a Go-style duration string. With the default
    /// <paramref name="options"/> the output matches Go's <c>time.Duration.String()</c>.
    /// Sub-tick precision (below 100 ns) is not representable by <see cref="TimeSpan"/>
    /// and is lost during parsing; values that survive parsing round-trip exactly through
    /// Format.
    /// </summary>
    public static string Format(TimeSpan value, DurationFormatOptions options = default)
    {
        var ticks = value.Ticks;
        if (ticks == 0)
            return "0s";

        var negative = ticks < 0;

        // `-ticks` overflows for long.MinValue in checked context; unchecked wraps back
        // to long.MinValue, and the ulong cast then produces the correct magnitude 2^63.
        var u = negative ? unchecked((ulong)(-ticks)) : (ulong)ticks;

        var sb = new StringBuilder();
        if (negative)
            sb.Append('-');
        else if (options.IncludePositiveSign)
            sb.Append('+');

        if (u < (ulong)TimeSpan.TicksPerSecond)
            FormatSubSecond(sb, u, options);
        else
            FormatSecondsAndAbove(sb, u, options);

        return sb.ToString();
    }

    private static void FormatSubSecond(StringBuilder sb, ulong ticks, DurationFormatOptions options)
    {
        if (ticks < 10) // < 1 µs
        {
            sb.Append(ticks * 100).Append("ns");
        }
        else if (ticks < 10_000) // < 1 ms
        {
            AppendWithFraction(sb, ticks * 100, prec: 3);
            sb.Append(options.MicrosecondSymbol == MicrosecondSymbol.Ascii ? "us" : "µs");
        }
        else // < 1 s
        {
            AppendWithFraction(sb, ticks * 100, prec: 6);
            sb.Append("ms");
        }
    }

    private static void FormatSecondsAndAbove(StringBuilder sb, ulong ticks, DurationFormatOptions options)
    {
        var wholeSeconds = ticks / (ulong)TimeSpan.TicksPerSecond;
        var fracTicks = ticks % (ulong)TimeSpan.TicksPerSecond;
        var fracNs = fracTicks * 100;

        var hours = wholeSeconds / 3600;
        var afterHours = wholeSeconds % 3600;
        var minutes = afterHours / 60;
        var seconds = afterHours % 60;

        // Base Go rules: hours only when non-zero, minutes when hours or minutes non-zero,
        // seconds always.
        var showHours = hours > 0;
        var showMinutes = hours > 0 || minutes > 0;
        var showSeconds = true;

        if (options.OmitZeroUnits)
        {
            if (hours == 0) showHours = false;
            if (minutes == 0) showMinutes = false;
            if (seconds == 0 && fracNs == 0) showSeconds = false;
        }

        if (showHours)
            sb.Append(hours).Append('h');
        if (showMinutes)
            sb.Append(minutes).Append('m');
        if (showSeconds)
        {
            sb.Append(seconds);
            if (fracNs > 0)
                AppendFractionalDigits(sb, fracNs, prec: 9);
            sb.Append('s');
        }
    }

    private static void AppendWithFraction(StringBuilder sb, ulong value, int prec)
    {
        ulong divisor = 1;
        for (var i = 0; i < prec; i++)
            divisor *= 10;

        var integer = value / divisor;
        var frac = value % divisor;

        sb.Append(integer);
        if (frac != 0)
            AppendFractionalDigits(sb, frac, prec);
    }

    private static void AppendFractionalDigits(StringBuilder sb, ulong frac, int prec)
    {
        Span<char> buf = stackalloc char[9];
        var slice = buf[..prec];
        var v = frac;
        for (var i = prec - 1; i >= 0; i--)
        {
            slice[i] = (char)('0' + v % 10);
            v /= 10;
        }

        var end = prec;
        while (end > 0 && slice[end - 1] == '0')
            end--;

        sb.Append('.');
        sb.Append(slice[..end]);
    }
}
