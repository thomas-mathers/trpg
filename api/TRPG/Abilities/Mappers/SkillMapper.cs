using ContractSkill = TRPG.Abilities.Responses.Skill;
using DataSkill = TRPG.Domain.Models.Skill;

namespace TRPG.Abilities.Mappers;

internal static class SkillMapper
{
    public static DataSkill ToDataModel(this ContractSkill skill) =>
        skill switch
        {
            ContractSkill.Melee => DataSkill.Melee,
            ContractSkill.Unarmed => DataSkill.Unarmed,
            ContractSkill.Sneak => DataSkill.Sneak,
            ContractSkill.Pickpocketing => DataSkill.Pickpocketing,
            ContractSkill.Destruction => DataSkill.Destruction,
            ContractSkill.Illusion => DataSkill.Illusion,
            ContractSkill.Archery => DataSkill.Archery,
            ContractSkill.Restoration => DataSkill.Restoration,
            ContractSkill.Alteration => DataSkill.Alteration,
            ContractSkill.General => DataSkill.General,
            ContractSkill.Blocking => DataSkill.Blocking,
            _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, null),
        };

    public static ContractSkill ToResponse(this DataSkill skill) =>
        skill switch
        {
            DataSkill.Melee => ContractSkill.Melee,
            DataSkill.Unarmed => ContractSkill.Unarmed,
            DataSkill.Sneak => ContractSkill.Sneak,
            DataSkill.Pickpocketing => ContractSkill.Pickpocketing,
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
