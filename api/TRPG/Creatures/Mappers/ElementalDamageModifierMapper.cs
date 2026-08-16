using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class ElementalDamageModifierMapper
{
    public static ElementalDamageModifierSummary ToSummary(this ElementalDamageModifier modifier) =>
        new(modifier.DamageType, modifier.MinDamage, modifier.MaxDamage);
}
