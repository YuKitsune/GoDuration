# GoDuration

A C# library that parses Go-style duration strings into `TimeSpan` values, and formats `TimeSpan` values back to Go-style duration strings.

## Packages

| Package | Purpose |
| --- | --- |
| `GoDuration` | Core parser and formatter. |
| `GoDuration.SystemTextJson` | `TimeSpan` converter for `System.Text.Json`. |
| `GoDuration.NewtonsoftJson` | `TimeSpan` converter for `Newtonsoft.Json`. |
| `GoDuration.YamlDotNet` | `TimeSpan` type converter for `YamlDotNet`. |

All packages target `netstandard2.0`, `net8.0`, and `net10.0`.

## Install

```bash
dotnet add package GoDuration
```

## Use

```csharp
using GoDuration;

TimeSpan value = Duration.Parse("1h30m45s");
string text = Duration.Format(value);
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

string text = Duration.Format(TimeSpan.FromHours(1), options);
```

## Serialization

### System.Text.Json

```csharp
using GoDuration.SystemTextJson;

var options = new JsonSerializerOptions
{
    Converters = { new GoDurationTimeSpanJsonConverter() }
};
```

See [`GoDuration.SystemTextJson`](https://github.com/YuKitsune/GoDuration/tree/main/source/GoDuration.SystemTextJson).

### Newtonsoft.Json

```csharp
using GoDuration.NewtonsoftJson;

var settings = new JsonSerializerSettings
{
    Converters = { new GoDurationTimeSpanJsonConverter() }
};
```

See [`GoDuration.NewtonsoftJson`](https://github.com/YuKitsune/GoDuration/tree/main/source/GoDuration.NewtonsoftJson).

### YamlDotNet

```csharp
using GoDuration.YamlDotNet;
using YamlDotNet.Serialization;

var serializer = new SerializerBuilder()
    .WithTypeConverter(new GoDurationTimeSpanYamlTypeConverter())
    .Build();
```

See [`GoDuration.YamlDotNet`](https://github.com/YuKitsune/GoDuration/tree/main/source/GoDuration.YamlDotNet).

## Build

The build uses [Fallout](https://fallout.build). Run one of the scripts at the repository root:

```bash
./build.sh Test
./build.sh Pack
```

Targets: `Clean`, `Restore`, `Compile`, `Test`, `Pack`, `BundleZip`, `PublishNuGet`, `PublishGitHubRelease`, `Publish`.

## License

MIT. See [LICENSE](LICENSE).
