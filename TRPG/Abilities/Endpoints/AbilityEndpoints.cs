using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TRPG.Application.Abilities;
using TRPG.Application.Abilities.Queries;
using TRPG.Application.Common.Mappers;
using TRPG.Contracts.Abilities.Responses;
using DataSkill = TRPG.Data.Models.Skill;

namespace TRPG.Abilities.Endpoints;

internal static class AbilityEndpoints
{
    public static void MapAbilityEndpoints(this WebApplication app)
    {
        app.MapGet("/abilities/{skill}", GetAbilitiesBySkill);
    }

    private static async Task<IResult> GetAbilitiesBySkill(
        Skill skill,
        GetAbilitiesBySkillQueryHandler getAbilitiesBySkill,
        CancellationToken cancellationToken
    )
    {
        var abilities = await getAbilitiesBySkill.Handle(
            new GetAbilitiesBySkillQuery { Skill = ToDomain(skill) },
            cancellationToken
        );

        return Results.Ok(abilities.Select(ToAbilitySummary).ToArray());
    }

    private static DataSkill ToDomain(Skill skill) =>
        skill switch
        {
            Skill.Melee => DataSkill.Melee,
            Skill.Unarmed => DataSkill.Unarmed,
            Skill.Sneak => DataSkill.Sneak,
            Skill.Destruction => DataSkill.Destruction,
            Skill.Illusion => DataSkill.Illusion,
            Skill.Archery => DataSkill.Archery,
            Skill.Restoration => DataSkill.Restoration,
            Skill.Alteration => DataSkill.Alteration,
            Skill.General => DataSkill.General,
            Skill.Blocking => DataSkill.Blocking,
            _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, null),
        };

    private static AbilitySummary ToAbilitySummary(Ability ability) =>
        new(
            ability.Name,
            ability.Skill.ToContract(),
            ability.Description,
            ability.ApCost,
            ability.MpCost,
            ability.Cooldown,
            ability is AttackAbility ? AbilityCategory.Offensive : AbilityCategory.Support
        );
}
