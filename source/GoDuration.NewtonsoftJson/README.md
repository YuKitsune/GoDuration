# GoDuration.NewtonsoftJson

A `Newtonsoft.Json` converter that reads and writes `TimeSpan` values as Go-style duration strings.

See the main [GoDuration](https://github.com/YuKitsune/GoDuration) README for the accepted duration grammar and the `DurationFormatOptions` reference.

## Install

```bash
dotnet add package GoDuration.NewtonsoftJson
```

## Use

Register the converter on a `JsonSerializerSettings`:

```csharp
using Newtonsoft.Json;
using GoDuration.NewtonsoftJson;

var settings = new JsonSerializerSettings
{
    Converters = { new GoDurationTimeSpanJsonConverter() }
};

string json = JsonConvert.SerializeObject(TimeSpan.FromMinutes(90), settings);
// "\"1h30m0s\""

TimeSpan value = JsonConvert.DeserializeObject<TimeSpan>("\"300ms\"", settings);
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
