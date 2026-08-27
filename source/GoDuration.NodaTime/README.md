# GoDuration.NodaTime

Reads and writes Go-style duration strings as `NodaTime.Duration` values through the [GoDuration](https://github.com/YuKitsune/GoDuration) parser and formatter.

## Install

```bash
dotnet add package GoDuration.NodaTime
```

## Use

```csharp
using GoDuration.NodaTime;
using NodaTime;

Duration value = GoDurationConverter.Parse("1h30m45s");
string text = GoDurationConverter.Format(value);
```

## Format options

```csharp
using GoDuration;
using GoDuration.NodaTime;
using NodaTime;

var options = new DurationFormatOptions
{
    OmitZeroUnits = true,
    MicrosecondSymbol = MicrosecondSymbol.Ascii,
};

string text = GoDurationConverter.Format(Duration.FromHours(1), options);
```

## Value range

The accepted range matches Go's `time.Duration`: signed int64 nanoseconds (about ±292 years). `Format` throws `OverflowException` for values outside this range.

## License

MIT. See the main [LICENSE](https://github.com/YuKitsune/GoDuration/blob/main/LICENSE).
