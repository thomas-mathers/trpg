using TRPG.Application.Abilities;
using AbilitySummary = TRPG.Contracts.Abilities.Responses.AbilitySummary;
using ContractAbilityCategory = TRPG.Contracts.Abilities.Responses.AbilityCategory;

namespace TRPG.Application.Common.Mappers;

public static class AbilitySummaryMapper
{
    public static AbilitySummary ToSummary(this Ability ability) =>
        new(
            ability.Name,
            ability.Skill.ToContract(),
            ability.Description,
            ability.ApCost,
            ability.MpCost,
            ability.Cooldown,
            ability is AttackAbility
                ? ContractAbilityCategory.Offensive
                : ContractAbilityCategory.Support,
            ability.RequiredSkillLevel,
            ability.Prerequisites
        );
}
