using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TRPG.Domain;

namespace TRPG.Data;

public sealed class GameInstantJsonConverter : JsonConverter<GameInstant>
{
    public override GameInstant Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) =>
        new(
            DateTime.Parse(
                reader.GetString()!,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind
            )
        );

    public override void Write(
        Utf8JsonWriter writer,
        GameInstant value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.Value.ToString("O", CultureInfo.InvariantCulture));
}
