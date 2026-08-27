using System.Globalization;

namespace GoDuration;

/// <summary>
/// Reads Go-style duration strings into a nanosecond count.
/// </summary>
/// <remarks>
/// See <see cref="GoDurationConverter"/> for the accepted grammar.
/// </remarks>
public static class GoDurationReader
{
    /// <summary>
    /// Tries to read <paramref name="input"/> as a Go-style duration string.
    /// </summary>
    /// <param name="input">The string to read.</param>
    /// <param name="nanoseconds">On success, the parsed value in nanoseconds. On failure, <c>0</c>.</param>
    /// <param name="error">
    /// On failure, the error message. On success, <see langword="null"/>.
    /// The message is one of: <c>invalid duration "…"</c>, <c>missing unit in duration "…"</c>,
    /// or <c>unknown unit "&lt;u&gt;" in duration "…"</c>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="input"/> was read; otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    public static bool TryRead(string input, out long nanoseconds, out string? error)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        nanoseconds = 0;
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

        var totalNanos = 0d;
        var anySegment = false;
        while (i < span.Length)
        {
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
            if (!TryMapUnit(unit, out var unitNanos))
            {
                error = $"unknown unit \"{unit.ToString()}\" in duration \"{input}\"";
                return false;
            }

            totalNanos += number * unitNanos;
            anySegment = true;
        }

        if (!anySegment)
        {
            error = InvalidDuration(input);
            return false;
        }

        try
        {
            nanoseconds = checked((long)Math.Round(sign * totalNanos));
            return true;
        }
        catch (OverflowException)
        {
            nanoseconds = 0;
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

    private static bool TryMapUnit(ReadOnlySpan<char> unit, out double nanosPerUnit)
    {
        nanosPerUnit = 0d;
        switch (unit.Length)
        {
            case 1:
                switch (unit[0])
                {
                    case 's': nanosPerUnit = 1_000_000_000d; return true;
                    case 'm': nanosPerUnit = 60_000_000_000d; return true;
                    case 'h': nanosPerUnit = 3_600_000_000_000d; return true;
                }
                break;
            case 2:
                if (unit[1] == 's')
                {
                    switch (unit[0])
                    {
                        case 'n': nanosPerUnit = 1d; return true;
                        case 'u': nanosPerUnit = 1_000d; return true;
                        case 'µ': nanosPerUnit = 1_000d; return true; // U+00B5
                        case 'μ': nanosPerUnit = 1_000d; return true; // U+03BC
                        case 'm': nanosPerUnit = 1_000_000d; return true;
                    }
                }
                break;
        }
        return false;
    }

    private static string InvalidDuration(string input) => $"invalid duration \"{input}\"";

    private static bool IsDigit(char c) => (uint)(c - '0') <= 9;
}
