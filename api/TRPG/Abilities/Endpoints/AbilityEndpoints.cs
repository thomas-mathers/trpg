using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TRPG.Application.Abilities.Mappers;
using TRPG.Application.Abilities.Queries;
using TRPG.Contracts.Abilities.Responses;
using DataSkill = TRPG.Data.Models.Skill;

namespace TRPG.Abilities.Endpoints;

internal static class AbilityEndpoints
{
    public static void MapAbilityEndpoints(this WebApplication app)
    {
        app.MapGet("/abilities/{skill}", GetAbilitiesBySkill).WithName("GetAbilitiesBySkill");
    }

    private static async Task<Ok<AbilitySummary[]>> GetAbilitiesBySkill(
        Skill skill,
        GetAbilitiesBySkillQueryHandler getAbilitiesBySkill,
        CancellationToken cancellationToken
    )
    {
        var abilities = await getAbilitiesBySkill.Handle(
            new GetAbilitiesBySkillQuery { Skill = ToDomain(skill) },
            cancellationToken
        );

        return TypedResults.Ok(abilities.Select(a => a.ToSummary()).ToArray());
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
}
