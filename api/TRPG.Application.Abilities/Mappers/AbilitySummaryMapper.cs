using AbilitySummary = TRPG.Contracts.Abilities.Responses.AbilitySummary;
using ContractAbilityCategory = TRPG.Contracts.Abilities.Responses.AbilityCategory;
using ContractSkill = TRPG.Contracts.Abilities.Responses.Skill;
using DataSkill = TRPG.Data.Models.Skill;

namespace TRPG.Application.Abilities.Mappers;

internal static class AbilitySummaryMapper
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

    public static ContractSkill ToContract(this DataSkill skill) =>
        skill switch
        {
            DataSkill.Melee => ContractSkill.Melee,
            DataSkill.Unarmed => ContractSkill.Unarmed,
            DataSkill.Sneak => ContractSkill.Sneak,
            DataSkill.Destruction => ContractSkill.Destruction,
            DataSkill.Illusion => ContractSkill.Illusion,
            DataSkill.Archery => ContractSkill.Archery,
            DataSkill.Restoration => ContractSkill.Restoration,
            DataSkill.Alteration => ContractSkill.Alteration,
            DataSkill.General => ContractSkill.General,
            DataSkill.Blocking => ContractSkill.Blocking,
            _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, null),
        };
}
