using System.Diagnostics.CodeAnalysis;

namespace GoDuration;

/// <summary>
/// Parses and formats Go-style duration strings for <see cref="TimeSpan"/> values.
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
///     A single <c>"0"</c> (or <c>"+0"</c>, <c>"-0"</c>) is accepted as zero. Every other input must be
///     one or more segments. A number without a unit (e.g. <c>"1"</c>) is rejected.
///   </description></item>
///   <item><description>
///     Segments can appear in any order and are added together (e.g. <c>"30s1m"</c> equals <c>"1m30s"</c>).
///   </description></item>
///   <item><description>
///     A number is an integer (<c>"1"</c>), an integer with a trailing dot (<c>"1."</c>), an integer with
///     a fraction (<c>"1.5"</c>), or a fraction only (<c>".5"</c>). Scientific notation is not accepted.
///   </description></item>
///   <item><description>
///     The accepted units are: <c>ns</c> (nanoseconds), <c>us</c> / <c>µs</c> (U+00B5) / <c>μs</c> (U+03BC)
///     (microseconds), <c>ms</c> (milliseconds), <c>s</c> (seconds), <c>m</c> (minutes), <c>h</c> (hours).
///     Day, week, month, and year units are not accepted.
///   </description></item>
///   <item><description>
///     Whitespace is not accepted anywhere in the string.
///   </description></item>
///   <item><description>
///     The accepted value range matches Go's <c>time.Duration</c>: signed int64 nanoseconds
///     (about ±292 years). Values outside this range are rejected as invalid.
///   </description></item>
///   <item><description>
///     The <see cref="TimeSpan"/> type cannot represent sub-tick precision (below 100 ns). Sub-tick
///     values round to the nearest tick (e.g. <c>"1ns"</c> parses to <see cref="TimeSpan.Zero"/>).
///   </description></item>
/// </list>
/// <para>Examples: <c>"300ms"</c>, <c>"-1.5h"</c>, <c>"2h45m"</c>, <c>"1h30m45s"</c>, <c>".5s"</c>.</para>
/// <para>
/// For direct access to the underlying nanosecond count, see <see cref="GoDurationReader"/> and
/// <see cref="GoDurationWriter"/>.
/// </para>
/// </remarks>
public static class GoDurationConverter
{
    /// <summary>Parses <paramref name="input"/> as a Go-style duration string.</summary>
    /// <param name="input">The string to parse.</param>
    /// <returns>The parsed <see cref="TimeSpan"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">
    /// <paramref name="input"/> does not match the accepted grammar or the value falls outside
    /// Go's signed int64 nanosecond range.
    /// </exception>
    public static TimeSpan Parse(string input)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        if (!GoDurationReader.TryRead(input, out var nanoseconds, out var error))
            throw new FormatException(error);

        return NanosecondsToTimeSpan(nanoseconds);
    }

    /// <summary>
    /// Tries to parse <paramref name="input"/> as a Go-style duration string. Returns
    /// <see langword="false"/> instead of throwing on any failure, including <see langword="null"/>
    /// and out-of-range values.
    /// </summary>
    /// <param name="input">The string to parse.</param>
    /// <param name="result">On success, the parsed value. On failure, <see cref="TimeSpan.Zero"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="input"/> was parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(
        [NotNullWhen(true)] string? input,
        out TimeSpan result)
    {
        if (input is null || !GoDurationReader.TryRead(input, out var nanoseconds, out _))
        {
            result = TimeSpan.Zero;
            return false;
        }

        result = NanosecondsToTimeSpan(nanoseconds);
        return true;
    }

    /// <summary>Formats <paramref name="value"/> as a Go-style duration string.</summary>
    /// <param name="value">The value to format.</param>
    /// <param name="options">Optional format settings. See <see cref="DurationFormatOptions"/>.</param>
    /// <returns>A duration string that <see cref="Parse"/> accepts.</returns>
    /// <exception cref="OverflowException">
    /// <paramref name="value"/> is outside Go's signed int64 nanosecond range (about ±292 years).
    /// </exception>
    public static string Format(TimeSpan value, DurationFormatOptions options = default)
    {
        var nanoseconds = checked(value.Ticks * 100L);
        return GoDurationWriter.Write(nanoseconds, options);
    }

    private static TimeSpan NanosecondsToTimeSpan(long nanoseconds)
    {
        // Round half-away-from-zero to the nearest 100-ns tick.
        if (nanoseconds >= 0)
            return TimeSpan.FromTicks((nanoseconds + 50) / 100);

        // Unchecked negation handles long.MinValue by wrapping to the correct unsigned magnitude 2^63.
        var magnitude = unchecked((ulong)(-nanoseconds));
        var tickMagnitude = (magnitude + 50UL) / 100UL;
        return TimeSpan.FromTicks(unchecked(-(long)tickMagnitude));
    }
}
