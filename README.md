# GoDuration

A C# library that parses Go-style duration strings into `TimeSpan` values, and formats `TimeSpan` values back to Go-style duration strings.

## Packages

| Package | Purpose |
| --- | --- |
| `GoDuration` | Core parser and formatter. Returns `TimeSpan`. |
| `GoDuration.NodaTime` | Parser and formatter that returns `NodaTime.Duration`. |

All packages target `netstandard2.0`, `net8.0`, and `net10.0`.

## Install

```bash
dotnet add package GoDuration
```

## Use

```csharp
using GoDuration;

TimeSpan value = GoDurationConverter.Parse("1h30m45s");
string text = GoDurationConverter.Format(value);
```

The parser accepts the same units as Go: `ns`, `us`, `µs`, `μs`, `ms`, `s`, `m`, and `h`.

## Format options

Use `DurationFormatOptions` to change the output format:

```csharp
var options = new DurationFormatOptions
{
    OmitZeroUnits = true,
    IncludePositiveSign = true,
    MicrosecondSymbol = MicrosecondSymbol.Ascii,
};

string text = GoDurationConverter.Format(TimeSpan.FromHours(1), options);
```

## Direct nanosecond access

Both packages sit on top of `GoDurationReader` and `GoDurationWriter`. Use these when
you want raw nanosecond counts and no value-type conversion:

```csharp
using GoDuration;

if (GoDurationReader.TryRead("1h30m", out long nanoseconds, out _))
{
    string text = GoDurationWriter.Write(nanoseconds);
}
```

## Value range

The accepted range matches Go's `time.Duration`: signed int64 nanoseconds (about ±292 years).
`Parse` rejects strings outside this range. `Format` throws `OverflowException` for `TimeSpan`
values outside this range.

## NodaTime

For `NodaTime.Duration` values, use the `GoDuration.NodaTime` package:

```csharp
using GoDuration.NodaTime;
using NodaTime;

Duration value = GoDurationConverter.Parse("1h30m45s");
string text = GoDurationConverter.Format(value);
```

See [`GoDuration.NodaTime`](https://github.com/YuKitsune/GoDuration/tree/main/source/GoDuration.NodaTime).

## Build

The build uses [Fallout](https://fallout.build). Run one of the scripts at the repository root:

```bash
./build.sh Test
./build.sh Pack
```

Targets: `Clean`, `Restore`, `Compile`, `Test`, `Pack`, `BundleZip`, `PublishNuGet`, `PublishGitHubRelease`, `Publish`.

## License

MIT. See [LICENSE](LICENSE).
