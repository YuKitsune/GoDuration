# GoDuration.YamlDotNet

A `YamlDotNet` type converter that reads and writes `TimeSpan` values as Go-style duration strings.

See the main [GoDuration](https://github.com/YuKitsune/GoDuration) README for the accepted duration grammar and the `DurationFormatOptions` reference.

## Install

```bash
dotnet add package GoDuration.YamlDotNet
```

## Use

Register the type converter on a `SerializerBuilder` and a `DeserializerBuilder`:

```csharp
using GoDuration.YamlDotNet;
using YamlDotNet.Serialization;

var serializer = new SerializerBuilder()
    .WithTypeConverter(new GoDurationTimeSpanYamlTypeConverter())
    .Build();

var deserializer = new DeserializerBuilder()
    .WithTypeConverter(new GoDurationTimeSpanYamlTypeConverter())
    .Build();

string yaml = serializer.Serialize(TimeSpan.FromMinutes(90));
// "1h30m0s\n"

TimeSpan value = deserializer.Deserialize<TimeSpan>("300ms");
// 300 milliseconds
```

## Format options

Pass a `DurationFormatOptions` value to change the write output:

```csharp
new GoDurationTimeSpanYamlTypeConverter(new DurationFormatOptions
{
    OmitZeroUnits = true,
});
```

## License

MIT.
