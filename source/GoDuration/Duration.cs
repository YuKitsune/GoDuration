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

        if (!TryParseCore(input, out var result, out var error))
            throw new FormatException(error);

        return result;
    }

    public static bool TryParse(
        [NotNullWhen(true)] string? input,
        out TimeSpan result)
    {
        if (input is null)
        {
            result = TimeSpan.Zero;
            return false;
        }

        return TryParseCore(input, out result, out _);
    }

    private static bool TryParseCore(string input, out TimeSpan result, out string? error)
    {
        result = TimeSpan.Zero;
        error = null;

        if (input.Length == 0)
        {
            error = InvalidDuration(input);
            return false;
        }

        var span = input.AsSpan();
        var i = 0;
        var sign = 1;
        if (span[0] == '+') { i = 1; }
        else if (span[0] == '-') { sign = -1; i = 1; }

        // Bare sign with nothing after it.
        if (i == span.Length)
        {
            error = InvalidDuration(input);
            return false;
        }

        // Go accepts a bare "0" (with no unit) as zero.
        if (span.Length - i == 1 && span[i] == '0')
            return true;

        var totalTicks = 0d;
        var anySegment = false;
        while (i < span.Length)
        {
            // Every segment must start with a digit or a dot.
            if (!IsDigit(span[i]) && span[i] != '.')
            {
                error = InvalidDuration(input);
                return false;
            }

            if (!TryReadNumber(span, ref i, out var number))
            {
                error = InvalidDuration(input);
                return false;
            }

            // Read the unit greedily so a stray character gets quoted in the message.
            var unitStart = i;
            while (i < span.Length && !IsDigit(span[i]) && span[i] != '.')
                i++;

            if (i == unitStart)
            {
                error = $"missing unit in duration \"{input}\"";
                return false;
            }

            var unit = span[unitStart..i];
            if (!TryMapUnit(unit, out var unitTicks))
            {
                error = $"unknown unit \"{unit}\" in duration \"{input}\"";
                return false;
            }

            totalTicks += number * unitTicks;
            anySegment = true;
        }

        if (!anySegment)
        {
            error = InvalidDuration(input);
            return false;
        }

        try
        {
            result = TimeSpan.FromTicks(checked((long)Math.Round(sign * totalTicks)));
            return true;
        }
        catch (OverflowException)
        {
            result = TimeSpan.Zero;
            error = InvalidDuration(input);
            return false;
        }
    }

    private static bool TryReadNumber(ReadOnlySpan<char> input, ref int i, out double value)
    {
        value = 0d;
        var start = i;

        while (i < input.Length && IsDigit(input[i]))
            i++;
        var hasIntegerDigits = i > start;

        var hasDot = false;
        var fractionStart = i;
        if (i < input.Length && input[i] == '.')
        {
            hasDot = true;
            i++;
            fractionStart = i;
            while (i < input.Length && IsDigit(input[i]))
                i++;
        }
        var hasFractionDigits = hasDot && i > fractionStart;

        if (!hasIntegerDigits && !hasFractionDigits)
            return false;

        return double.TryParse(input[start..i], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryMapUnit(ReadOnlySpan<char> unit, out double ticksPerUnit)
    {
        ticksPerUnit = 0d;
        switch (unit.Length)
        {
            case 1:
                switch (unit[0])
                {
                    case 's': ticksPerUnit = 10_000_000d; return true;
                    case 'm': ticksPerUnit = 600_000_000d; return true;
                    case 'h': ticksPerUnit = 36_000_000_000d; return true;
                }
                break;
            case 2:
                if (unit[1] == 's')
                {
                    switch (unit[0])
                    {
                        case 'n': ticksPerUnit = 0.01d; return true;
                        case 'u': ticksPerUnit = 10d; return true;
                        case 'µ': ticksPerUnit = 10d; return true; // U+00B5
                        case 'μ': ticksPerUnit = 10d; return true; // U+03BC
                        case 'm': ticksPerUnit = 10_000d; return true;
                    }
                }
                break;
        }
        return false;
    }

    private static string InvalidDuration(string input) => $"invalid duration \"{input}\"";

    private static bool IsDigit(char c) => (uint)(c - '0') <= 9;

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
