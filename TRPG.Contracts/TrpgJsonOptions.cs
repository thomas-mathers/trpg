using System.Text.Json;
using System.Text.Json.Serialization;

namespace TRPG.Contracts;

public static class TrpgJsonOptions
{
    public static JsonSerializerOptions Default { get; } =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
        };
}
