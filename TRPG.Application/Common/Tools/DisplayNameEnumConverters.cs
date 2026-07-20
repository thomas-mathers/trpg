using System.Text.Json;
using System.Text.Json.Serialization;
using TRPG.Application.Common.Mappers;
using TRPG.Contracts;
using TRPG.Data.Models;
using static TRPG.Application.Common.Tools.DisplayNameEnumConverterHelper;
using ConditionType = TRPG.Application.Abilities.ConditionType;

namespace TRPG.Application.Common.Tools;

internal sealed class GenderJsonConverter : JsonConverter<Gender>
{
    public override Gender Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => ParseDisplayName<Gender>(reader.GetString());

    public override void Write(Utf8JsonWriter writer, Gender value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToContract().ToDisplayName());
}

internal sealed class CreatureTypeJsonConverter : JsonConverter<CreatureType>
{
    public override CreatureType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => ParseDisplayName<CreatureType>(reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        CreatureType value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToContract().ToDisplayName());
}

internal sealed class ProfessionJsonConverter : JsonConverter<Profession>
{
    public override Profession Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => ParseDisplayName<Profession>(reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        Profession value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToContract().ToDisplayName());
}

internal sealed class CreatureStateJsonConverter : JsonConverter<CreatureState>
{
    public override CreatureState Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => ParseDisplayName<CreatureState>(reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        CreatureState value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToContract().ToDisplayName());
}

internal sealed class DistrictTypeJsonConverter : JsonConverter<DistrictType>
{
    public override DistrictType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => ParseDisplayName<DistrictType>(reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        DistrictType value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToContract().ToDisplayName());
}

internal sealed class BuildingTypeJsonConverter : JsonConverter<BuildingType>
{
    public override BuildingType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => ParseDisplayName<BuildingType>(reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        BuildingType value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToContract().ToDisplayName());
}

internal sealed class DamageTypeJsonConverter : JsonConverter<DamageType>
{
    public override DamageType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => ParseDisplayName<DamageType>(reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        DamageType value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToContract().ToDisplayName());
}

internal sealed class AttributeNameJsonConverter : JsonConverter<AttributeName>
{
    public override AttributeName Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => ParseDisplayName<AttributeName>(reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        AttributeName value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToContract().ToDisplayName());
}

internal sealed class AmountTypeJsonConverter : JsonConverter<AmountType>
{
    public override AmountType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => ParseDisplayName<AmountType>(reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        AmountType value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToContract().ToDisplayName());
}

internal sealed class ConditionTypeJsonConverter : JsonConverter<ConditionType>
{
    public override ConditionType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => ParseDisplayName<ConditionType>(reader.GetString());

    public override void Write(
        Utf8JsonWriter writer,
        ConditionType value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToContract().ToDisplayName());
}

internal static class DisplayNameEnumConverterHelper
{
    public static TEnum ParseDisplayName<TEnum>(string? text)
        where TEnum : struct, Enum
    {
        foreach (var value in Enum.GetValues<TEnum>())
        {
            if (string.Equals(value.ToDisplayName(), text, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return default;
    }
}
