using System.Text.Json;
using System.Text.Json.Serialization;

namespace TRPG.Tools;

internal static class ToolJsonOptions
{
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
