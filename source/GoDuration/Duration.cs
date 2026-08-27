using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Superpower;
using Superpower.Parsers;

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

        try
        {
            var parsed = DurationParser.TryParse(input);
            if (!parsed.HasValue || !parsed.Remainder.IsAtEnd)
                return false;
            
            result = parsed.Value;
            return true;
        }
        catch (OverflowException) { return false; }
        catch (ArgumentOutOfRangeException) { return false; }
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

    private static readonly TextParser<int> Sign =
        Character.EqualTo('+').Value(1)
            .Or(Character.EqualTo('-').Value(-1))
            .OptionalOrDefault(1);

    // ".5" — dot followed by at least one digit.
    private static readonly TextParser<double> FractionOnly =
        from _ in Character.EqualTo('.')
        from digits in Character.Digit.AtLeastOnce()
        select double.Parse("." + new string(digits), CultureInfo.InvariantCulture);

    // "1", "1.", "1.5" — leading digits, optional dot with optional trailing digits.
    private static readonly TextParser<double> IntegerWithOptionalFraction =
        from before in Character.Digit.AtLeastOnce()
        from after in (
                from _ in Character.EqualTo('.')
                from d in Character.Digit.Many()
                select "." + new string(d)
            ).OptionalOrDefault(string.Empty)
        select double.Parse(new string(before) + after, CultureInfo.InvariantCulture);

    private static readonly TextParser<double> Number =
        FractionOnly.Try().Or(IntegerWithOptionalFraction);

    // A TimeSpan tick is 100 nanoseconds. Values are kept as double so that
    // fractional units (e.g. "0.5ms") do not lose precision until the final cast.
    private static readonly TextParser<double> UnitTicks =
        Span.EqualTo("ns").Value(0.01d)
            .Or(Span.EqualTo("us").Value(10d))
            .Or(Span.EqualTo("µs").Value(10d)) // µs (U+00B5)
            .Or(Span.EqualTo("μs").Value(10d)) // μs (U+03BC)
            .Or(Span.EqualTo("ms").Value(10_000d))
            .Or(Span.EqualTo("s").Value(10_000_000d))
            .Or(Span.EqualTo("m").Value(600_000_000d))
            .Or(Span.EqualTo("h").Value(36_000_000_000d));

    private static readonly TextParser<double> Segment =
        from num in Number
        from unit in UnitTicks
        select num * unit;

    private static readonly TextParser<double> Segments =
        Segment.Try().AtLeastOnce().Select(s => s.Sum());

    private static readonly TextParser<double> BareZero =
        Character.EqualTo('0').Value(0d);

    private static readonly TextParser<TimeSpan> DurationParser =
        from sign in Sign
        from ticks in Segments.Try().Or(BareZero)
        select TimeSpan.FromTicks(checked((long)Math.Round(sign * ticks)));
}
