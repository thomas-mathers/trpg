using System.Text.Json;
using System.Text.Json.Serialization;

namespace TRPG.Application.Tools.Common;

internal static class ToolJsonOptions
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Default)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}
