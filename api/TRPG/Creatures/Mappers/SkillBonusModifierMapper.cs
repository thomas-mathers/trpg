using TRPG.Abilities.Mappers;
using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class SkillBonusModifierMapper
{
    public static SkillBonusModifierSummary ToSummary(this SkillBonusModifier modifier) =>
        new(modifier.Amount, modifier.Skill?.ToContract());
}
