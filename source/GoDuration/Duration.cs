using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
