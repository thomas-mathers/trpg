using TRPG.Abilities.Mappers;
using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class SkillBonusModifierMapper
{
    public static SkillBonusModifierSummary ToSummary(this SkillBonusModifier modifier) =>
        new(modifier.Amount, modifier.Skill?.ToResponse());
}
