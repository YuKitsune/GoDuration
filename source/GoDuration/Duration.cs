using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace GoDuration;

/// <summary>
/// Parses and formats Go-style duration strings.
/// </summary>
/// <remarks>
/// <para>
/// The accepted grammar is:
/// </para>
/// <code>
/// duration = ["+" | "-"] ( "0" | segment+ )
/// segment  = number unit
/// number   = digits ("." digits?)? | "." digits
/// unit     = "ns" | "us" | "µs" | "μs" | "ms" | "s" | "m" | "h"
/// </code>
/// <list type="bullet">
///   <item><description>An optional leading <c>+</c> or <c>-</c> sets the sign.</description></item>
///   <item><description>
///     A bare <c>"0"</c> (or <c>"+0"</c>, <c>"-0"</c>) is accepted as zero. Every other input must be one
///     or more segments; a plain number without a unit (e.g. <c>"1"</c>) is rejected.
///   </description></item>
///   <item><description>
///     Segments may appear in any order and are summed (e.g. <c>"30s1m"</c> equals <c>"1m30s"</c>).
///   </description></item>
///   <item><description>
///     Numbers may be integer (<c>"1"</c>), integer with trailing dot (<c>"1."</c>), integer with fraction
///     (<c>"1.5"</c>) or fraction only (<c>".5"</c>). Scientific notation is not accepted.
///   </description></item>
///   <item><description>
///     Recognised units are: <c>ns</c> (nanoseconds), <c>us</c> / <c>µs</c> (U+00B5) / <c>μs</c> (U+03BC)
///     (microseconds), <c>ms</c> (milliseconds), <c>s</c> (seconds), <c>m</c> (minutes), <c>h</c> (hours).
///     No day, week, month, or year units.
///   </description></item>
///   <item><description>
///     Whitespace is not accepted anywhere in the string.
///   </description></item>
///   <item><description>
///     Sub-tick precision (below 100 ns) is not representable by <see cref="TimeSpan"/> and rounds toward
///     the nearest tick (e.g. <c>"1ns"</c> parses to <see cref="TimeSpan.Zero"/>).
///   </description></item>
/// </list>
/// <para>Examples: <c>"300ms"</c>, <c>"-1.5h"</c>, <c>"2h45m"</c>, <c>"1h30m45s"</c>, <c>".5s"</c>.</para>
/// </remarks>
public static class Duration
{
    /// <summary>
    /// Parses <paramref name="input"/> as a Go-style duration string.
    /// </summary>
    /// <param name="input">The string to parse. See <see cref="Duration"/> for the accepted grammar.</param>
    /// <returns>The parsed <see cref="TimeSpan"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">
    /// <paramref name="input"/> does not match the accepted grammar or the value overflows <see cref="TimeSpan"/>.
    /// The message is one of: <c>invalid duration "…"</c>, <c>missing unit in duration "…"</c>, or
    /// <c>unknown unit "&lt;u&gt;" in duration "…"</c>.
    /// </exception>
    public static TimeSpan Parse(string input)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        if (!TryParseCore(input, out var result, out var error))
            throw new FormatException(error);

        return result;
    }

    /// <summary>
    /// Tries to parse <paramref name="input"/> as a Go-style duration string. Returns <see langword="false"/>
    /// (rather than throwing) on any failure, including <see langword="null"/> and overflow.
    /// </summary>
    /// <param name="input">The string to parse. See <see cref="Duration"/> for the accepted grammar.</param>
    /// <param name="result">
    /// On success, the parsed value. On failure, <see cref="TimeSpan.Zero"/>.
    /// </param>
    /// <returns><see langword="true"/> if <paramref name="input"/> was parsed; otherwise <see langword="false"/>.</returns>
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

            var unit = span.Slice(unitStart, i - unitStart);
            if (!TryMapUnit(unit, out var unitTicks))
            {
                error = $"unknown unit \"{unit.ToString()}\" in duration \"{input}\"";
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

        var numberSlice = input.Slice(start, i - start);
#if NETSTANDARD2_0
        return double.TryParse(numberSlice.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#else
        return double.TryParse(numberSlice, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#endif
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
    /// Renders <paramref name="value"/> as a Go-style duration string.
    /// </summary>
    /// <param name="value">The value to render.</param>
    /// <param name="options">
    /// Optional formatting toggles. See <see cref="DurationFormatOptions"/>. With the default value,
    /// the output is identical to what <see cref="Parse"/> would round-trip.
    /// </param>
    /// <returns>A duration string that <see cref="Parse"/> accepts.</returns>
    /// <remarks>
    /// <para>Output rules (with the default <paramref name="options"/>):</para>
    /// <list type="bullet">
    ///   <item><description>Zero always renders as <c>"0s"</c>.</description></item>
    ///   <item><description>Negative values are prefixed with <c>-</c>. Positive values carry no sign.</description></item>
    ///   <item><description>
    ///     Values below one second use a single sub-second unit: <c>ns</c> below one microsecond,
    ///     <c>µs</c> below one millisecond, <c>ms</c> below one second. Fractional digits are emitted
    ///     only when non-zero and trailing zeros are trimmed (e.g. <c>1500 ticks</c> → <c>"150µs"</c>,
    ///     <c>1</c> tick → <c>"100ns"</c>).
    ///   </description></item>
    ///   <item><description>
    ///     Values at or above one second render as <c>h</c>, <c>m</c>, <c>s</c> combined. Hours appear only
    ///     when non-zero; minutes appear when hours or minutes are non-zero; seconds always appear so the
    ///     output has a unit (e.g. <c>"1m0s"</c>, <c>"1h0m0s"</c>). Fractional seconds are appended as up to
    ///     nine digits with trailing zeros trimmed (e.g. <c>"1.5s"</c>).
    ///   </description></item>
    /// </list>
    /// <para>
    /// Options change this:
    /// <see cref="DurationFormatOptions.OmitZeroUnits"/> drops zero components (e.g. <c>"1h"</c> instead of
    /// <c>"1h0m0s"</c>); <see cref="DurationFormatOptions.IncludePositiveSign"/> prefixes positive values with
    /// <c>+</c>; <see cref="DurationFormatOptions.MicrosecondSymbol"/> switches between <c>µs</c> and <c>us</c>
    /// for microseconds.
    /// </para>
    /// <para>
    /// Sub-tick precision (below 100 ns) is not representable by <see cref="TimeSpan"/> and is discarded at
    /// parse time; any value that survives <see cref="Parse"/> round-trips through <c>Format</c> exactly.
    /// </para>
    /// </remarks>
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
