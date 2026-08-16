using TRPG.Contracts.Creatures.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class AttributesMapper
{
    public static EffectiveAttributesResponse ToResponse(this Attributes attributes) =>
        new(
            attributes.Strength,
            attributes.Dexterity,
            attributes.Intelligence,
            attributes.Endurance,
            attributes.Stamina,
            attributes.Mana,
            attributes.Defense,
            attributes.MaximumHp,
            attributes.MaximumAp,
            attributes.MaximumMp,
            attributes.MovementSpeed,
            attributes.PhysicalResistance,
            attributes.FireResistance,
            attributes.IceResistance,
            attributes.LightningResistance,
            attributes.PoisonResistance,
            attributes.MagicResistance
        );
}
