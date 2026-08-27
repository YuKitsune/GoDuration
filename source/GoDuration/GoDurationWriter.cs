using System.Text;

namespace GoDuration;

/// <summary>
/// Writes a nanosecond count as a Go-style duration string.
/// </summary>
/// <remarks>
/// See <see cref="GoDurationConverter"/> for the output format.
/// </remarks>
public static class GoDurationWriter
{
    /// <summary>
    /// Writes <paramref name="nanoseconds"/> as a Go-style duration string.
    /// </summary>
    /// <param name="nanoseconds">The value in nanoseconds.</param>
    /// <param name="options">
    /// Optional format settings. See <see cref="DurationFormatOptions"/>. With the default value,
    /// the output is exactly what <see cref="GoDurationReader.TryRead"/> accepts.
    /// </param>
    /// <returns>A duration string that <see cref="GoDurationReader.TryRead"/> accepts.</returns>
    public static string Write(long nanoseconds, DurationFormatOptions options = default)
    {
        if (nanoseconds == 0)
            return "0s";

        var negative = nanoseconds < 0;

        // Unchecked negation at long.MinValue wraps to the correct unsigned magnitude 2^63.
        var u = negative ? unchecked((ulong)(-nanoseconds)) : (ulong)nanoseconds;

        var sb = new StringBuilder();
        if (negative)
            sb.Append('-');
        else if (options.IncludePositiveSign)
            sb.Append('+');

        if (u < 1_000_000_000UL) // < 1 s
            WriteSubSecond(sb, u, options);
        else
            WriteSecondsAndAbove(sb, u, options);

        return sb.ToString();
    }

    private static void WriteSubSecond(StringBuilder sb, ulong nanoseconds, DurationFormatOptions options)
    {
        if (nanoseconds < 1_000UL) // < 1 µs
        {
            sb.Append(nanoseconds).Append("ns");
        }
        else if (nanoseconds < 1_000_000UL) // < 1 ms
        {
            AppendWithFraction(sb, nanoseconds, prec: 3);
            sb.Append(options.MicrosecondSymbol == MicrosecondSymbol.Ascii ? "us" : "µs");
        }
        else // < 1 s
        {
            AppendWithFraction(sb, nanoseconds, prec: 6);
            sb.Append("ms");
        }
    }

    private static void WriteSecondsAndAbove(StringBuilder sb, ulong nanoseconds, DurationFormatOptions options)
    {
        const ulong NanosPerSecond = 1_000_000_000UL;
        var wholeSeconds = nanoseconds / NanosPerSecond;
        var fracNanos = nanoseconds % NanosPerSecond;

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
            if (seconds == 0 && fracNanos == 0) showSeconds = false;
        }

        if (showHours)
            sb.Append(hours).Append('h');
        if (showMinutes)
            sb.Append(minutes).Append('m');
        if (showSeconds)
        {
            sb.Append(seconds);
            if (fracNanos > 0)
                AppendFractionalDigits(sb, fracNanos, prec: 9);
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
        var slice = buf.Slice(0, prec);
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
        var trimmed = slice.Slice(0, end);
#if NETSTANDARD2_0
        sb.Append(trimmed.ToString());
#else
        sb.Append(trimmed);
#endif
    }
}
