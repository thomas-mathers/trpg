using TRPG.Contracts.Inventory.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class ElementalDamageModifierMapper
{
    public static ElementalDamageModifierSummary ToSummary(this ElementalDamageModifier modifier) =>
        new(modifier.DamageType.ToContract(), modifier.MinDamage, modifier.MaxDamage);
}
