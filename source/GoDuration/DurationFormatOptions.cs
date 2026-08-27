namespace GoDuration;

/// <summary>
/// How the microsecond unit is formatted. <see cref="Mu"/> is the Go default.
/// </summary>
public enum MicrosecondSymbol : byte
{
    /// <summary>The Greek letter <c>µ</c> (U+00B5). Matches Go's default output.</summary>
    Mu = 0,

    /// <summary>ASCII <c>u</c>. Also accepted by the parser.</summary>
    Ascii = 1
}

/// <summary>
/// Format options for <see cref="Duration.Format(TimeSpan, DurationFormatOptions)"/>.
/// The default value produces Go's <c>time.Duration.String()</c> output.
/// </summary>
public readonly record struct DurationFormatOptions
{
    /// <summary>The default options. Output matches Go's <c>time.Duration.String()</c>.</summary>
    public static DurationFormatOptions Default => default;

    /// <summary>When true, positive non-zero values get a <c>+</c> prefix.</summary>
    public bool IncludePositiveSign { get; init; }

    /// <summary>The glyph to use for the microsecond unit.</summary>
    public MicrosecondSymbol MicrosecondSymbol { get; init; }

    /// <summary>
    /// When true, zero-valued unit segments in the seconds-and-above output are removed.
    /// A total-zero value still outputs <c>"0s"</c>. Sub-second output has a single segment
    /// and is not affected.
    /// </summary>
    public bool OmitZeroUnits { get; init; }
}
