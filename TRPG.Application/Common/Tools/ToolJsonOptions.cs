using System.Text.Json;
using System.Text.Json.Serialization;

namespace TRPG.Application.Common.Tools;

internal static class ToolJsonOptions
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Default)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new GenderJsonConverter(),
            new CreatureTypeJsonConverter(),
            new ProfessionJsonConverter(),
            new CreatureStateJsonConverter(),
            new DistrictTypeJsonConverter(),
            new BuildingTypeJsonConverter(),
            new DamageTypeJsonConverter(),
            new AttributeNameJsonConverter(),
            new AmountTypeJsonConverter(),
            new ConditionTypeJsonConverter(),
        },
    };
}
