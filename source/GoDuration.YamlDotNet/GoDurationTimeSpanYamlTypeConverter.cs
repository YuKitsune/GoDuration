using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace GoDuration.YamlDotNet;

/// <summary>
/// YamlDotNet type converter that reads and writes <see cref="TimeSpan"/> values
/// as Go-style duration strings. Register via
/// <c>SerializerBuilder.WithTypeConverter</c> and <c>DeserializerBuilder.WithTypeConverter</c>.
/// The <see cref="DurationFormatOptions"/> passed to the constructor control the
/// write-side formatting; parsing is single-mode.
/// </summary>
public sealed class GoDurationTimeSpanYamlTypeConverter : IYamlTypeConverter
{
    private readonly DurationFormatOptions _formatOptions;

    public GoDurationTimeSpanYamlTypeConverter()
        : this(DurationFormatOptions.Default)
    {
    }

    public GoDurationTimeSpanYamlTypeConverter(DurationFormatOptions formatOptions)
    {
        _formatOptions = formatOptions;
    }

    public bool Accepts(Type type) => type == typeof(TimeSpan);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();

        try
        {
            return Duration.Parse(scalar.Value);
        }
        catch (FormatException ex)
        {
            throw new YamlException(scalar.Start, scalar.End, ex.Message, ex);
        }
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var ts = (TimeSpan)value!;
        emitter.Emit(new Scalar(Duration.Format(ts, _formatOptions)));
    }
}
