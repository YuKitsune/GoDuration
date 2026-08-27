# GoDuration.SystemTextJson

A `System.Text.Json` converter that reads and writes `TimeSpan` values as Go-style duration strings.

See the main [GoDuration](https://github.com/YuKitsune/GoDuration) README for the accepted duration grammar and the `DurationFormatOptions` reference.

## Install

```bash
dotnet add package GoDuration.SystemTextJson
```

## Use

Register the converter on a `JsonSerializerOptions`:

```csharp
using System.Text.Json;
using GoDuration.SystemTextJson;

var options = new JsonSerializerOptions
{
    Converters = { new GoDurationTimeSpanJsonConverter() }
};

string json = JsonSerializer.Serialize(TimeSpan.FromMinutes(90), options);
// "1h30m0s"

TimeSpan value = JsonSerializer.Deserialize<TimeSpan>("\"300ms\"", options);
// 300 milliseconds
```

The converter reads only string JSON tokens. Numbers, `null`, objects, and arrays are rejected.

## Format options

Pass a `DurationFormatOptions` value to change the write output:

```csharp
new GoDurationTimeSpanJsonConverter(new DurationFormatOptions
{
    OmitZeroUnits = true,
});
```

## License

MIT.
