using NodaTime;

namespace GoDuration.NodaTime;

/// <summary>
/// Parses and formats Go-style duration strings for <see cref="Duration"/> values.
/// </summary>
/// <remarks>
/// <para>
/// See <see cref="global::GoDuration.GoDurationConverter"/> for the accepted grammar and format rules.
/// </para>
/// <para>
/// The accepted value range matches Go's <c>time.Duration</c>: signed int64 nanoseconds
/// (about ±292 years). <see cref="Format(Duration, DurationFormatOptions)"/> throws
/// <see cref="OverflowException"/> for values outside this range.
/// </para>
/// <para>
/// For direct access to the underlying nanosecond count, see
/// <see cref="global::GoDuration.GoDurationReader"/> and
/// <see cref="global::GoDuration.GoDurationWriter"/>.
/// </para>
/// </remarks>
public static class GoDurationConverter
{
    /// <summary>Parses <paramref name="input"/> as a Go-style duration string.</summary>
    /// <param name="input">The string to parse.</param>
    /// <returns>The parsed <see cref="Duration"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">
    /// <paramref name="input"/> does not match the accepted grammar or the value falls outside
    /// Go's signed int64 nanosecond range.
    /// </exception>
    public static Duration Parse(string input)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        if (!GoDurationReader.TryRead(input, out var nanoseconds, out var error))
            throw new FormatException(error);

        return Duration.FromNanoseconds(nanoseconds);
    }

    /// <summary>
    /// Tries to parse <paramref name="input"/> as a Go-style duration string. Returns
    /// <see langword="false"/> instead of throwing on any failure, including <see langword="null"/>
    /// and out-of-range values.
    /// </summary>
    /// <param name="input">The string to parse.</param>
    /// <param name="result">On success, the parsed value. On failure, <see cref="Duration.Zero"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="input"/> was parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? input, out Duration result)
    {
        if (input is null || !GoDurationReader.TryRead(input, out var nanoseconds, out _))
        {
            result = Duration.Zero;
            return false;
        }

        result = Duration.FromNanoseconds(nanoseconds);
        return true;
    }

    /// <summary>Formats <paramref name="value"/> as a Go-style duration string.</summary>
    /// <param name="value">The value to format.</param>
    /// <param name="options">Optional format settings. See <see cref="DurationFormatOptions"/>.</param>
    /// <returns>A duration string that <see cref="Parse"/> accepts.</returns>
    /// <exception cref="OverflowException">
    /// <paramref name="value"/> is outside Go's signed int64 nanosecond range (about ±292 years).
    /// </exception>
    public static string Format(Duration value, DurationFormatOptions options = default)
    {
        var nanoseconds = value.ToInt64Nanoseconds();
        return GoDurationWriter.Write(nanoseconds, options);
    }
}
