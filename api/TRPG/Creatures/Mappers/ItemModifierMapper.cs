using TRPG.Contracts.Inventory.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class ItemModifierMapper
{
    public static ItemModifierSummary ToSummary(this ItemModifier modifier) =>
        modifier switch
        {
            AttributeModifier attributeModifier => attributeModifier.ToSummary(),
            CombatSpeedModifier combatSpeedModifier => combatSpeedModifier.ToSummary(),
            ElementalDamageModifier elementalDamageModifier => elementalDamageModifier.ToSummary(),
            LeechModifier leechModifier => leechModifier.ToSummary(),
            SpecialHitModifier specialHitModifier => specialHitModifier.ToSummary(),
            SkillBonusModifier skillBonusModifier => skillBonusModifier.ToSummary(),
            ProcModifier procModifier => procModifier.ToSummary(),
            _ => throw new ArgumentOutOfRangeException(nameof(modifier)),
        };
}
